using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using MPLATFORMLib;

namespace NdiMl
{
    public partial class NdiSource : ObservableObject
    {
        private MLiveClass? _source;
        private Thread? _ndiFinderThread;

        [ObservableProperty]
        private int sourceId;

        private bool runThread;

        [ObservableProperty]
        private int _sourcesFoundCount;

        [ObservableProperty]
        private ObservableCollection<string> _sources = new();

        [ObservableProperty]
        private ICollectionView _sourcesView;


        public NdiSource(int id)
        {
            sourceId = id;
            _sourcesView = CollectionViewSource.GetDefaultView(_sources);
            _sourcesView.SortDescriptions.Add(new SortDescription());
        }

        public Task InitSource()
        {
            return Task.Run(() =>
            {
                _source = new MLiveClass();
                _source.DeviceSet("video", "NDI Receiver", "");
                _source.ObjectStart(null);


                var sb = new StringBuilder();

                _ndiFinderThread = new Thread(() =>
                {
                    while (runThread)
                    {
                        _source.PropsGet("device::stat::ndi_find_src", out var srcCount);

                        if (int.TryParse(srcCount, out var sourcesFoundCount))
                            SourcesFoundCount = sourcesFoundCount;

                        _source.PropsGet("device::line-in-split", out var lineSplit);
                        if (lineSplit != null)
                        {
                            App.Current.Dispatcher.BeginInvoke(() =>
                            {
                                Sources.Clear();
                            });

                            sb.Clear();
                            sb.AppendLine($"{SourceId} => {srcCount}");
                            var lines = lineSplit.Split('|');
                            foreach (var line in lines)
                            {
                                App.Current.Dispatcher.BeginInvoke(() =>
                                {
                                    Sources.Add(line);
                                });
                            }

                            Debug.WriteLine(sb.ToString());
                        }
                        Thread.Sleep(1000);
                    }

                });
                runThread = true;
                _ndiFinderThread.Start();
            });
        }

        public Task Cleanup()
        {
            return Task.Run(() =>
            {
                runThread = false;
                _ndiFinderThread?.Join();

                if (_source != null)
                    Marshal.ReleaseComObject(_source);
            });
        }

    }
}
