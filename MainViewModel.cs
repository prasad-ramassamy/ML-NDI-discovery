using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NdiMl
{
    public partial class MainViewModel : ObservableObject
    {
        private NdiSource _ndiFinder;

        public MainViewModel()
        {
            _ndiFinder = new(0);

            _ndiFinder.InitSource(null);

            _ndiFinder.Sources.CollectionChanged += Sources_CollectionChanged;

        }

        private void Sources_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            //if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            //    AvailableSources.Clear();

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (var newItem in e.NewItems)
                {
                    var newItemString = newItem.ToString();
                    if (newItemString != null && !AvailableSources.Contains(newItemString))
                        AvailableSources.Add(newItemString);
                }

            }

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (var oldItem in e.OldItems)
                {
                    if (oldItem != null)
                        AvailableSources.Remove(oldItem.ToString() ?? "");
                }

            }
        }


        [ObservableProperty]
        private ObservableCollection<string> _availableSources = new ObservableCollection<string>();

        [ObservableProperty]
        private string? _selectedLine;


        [ObservableProperty]
        private ObservableCollection<NdiSource> _sources = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        [NotifyCanExecuteChangedFor(nameof(SetLineCommand))]
        private bool _canStop;

        [ObservableProperty]
        private string sourcesCount = "4";

        [ObservableProperty]
        private bool _parallelStart = true;

        [ObservableProperty]
        private bool _connectAtStart = true;


        [RelayCommand(AllowConcurrentExecutions = false)]
        private async Task Start(CancellationToken cancelStart)
        {
            if (!int.TryParse(SourcesCount, out var nbSources))
                return;

            if (nbSources < 1 || nbSources > 10)
            {
                MessageBox.Show("Number of sources must be beetween 1 and 10");
                return;
            }

            CanStop = false;
            await Stop();

            for (int i = 1; i <= nbSources; i++)
            {
                Sources.Add(new NdiSource(i));
            }

            if (ParallelStart)
            {
                ParallelOptions parallelOptions = new()
                {
                    MaxDegreeOfParallelism = nbSources
                };

                await Parallel.ForEachAsync(Sources, parallelOptions, async (source, cancelStart) =>
                {
                    await source.InitSource(ConnectAtStart ? SelectedLine : null);
                });
            }
            else
            {
                foreach (NdiSource source in Sources)
                {
                    await source.InitSource(ConnectAtStart ? SelectedLine : null);
                }
            }
            CanStop = true;
        }

        [RelayCommand(AllowConcurrentExecutions = false, CanExecute = nameof(CanStop))]
        public async Task Stop()
        {
            await Parallel.ForEachAsync(Sources, async (s, token) =>
            {
                await s.Cleanup();
            });

            Sources.Clear();
            CanStop = false;
        }

        [RelayCommand(CanExecute = nameof(CanStop))]
        private void SetLine()
        {
            if (!CanStop) return;

            if (SelectedLine == null) return;

            foreach (var s in Sources)
                s.SetLine(SelectedLine);
        }
    }
}
