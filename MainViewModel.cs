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
        [ObservableProperty]
        private ObservableCollection<NdiSource> _sources = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        private bool _canStop;

        [ObservableProperty]
        private string sourcesCount = "4";

        [ObservableProperty]
        private bool _parallelStart = true;


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
                    await source.InitSource();
                });
            }
            else
            {
                foreach (NdiSource source in Sources)
                {
                    await source.InitSource();
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
    }
}
