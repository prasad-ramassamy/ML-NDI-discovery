using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
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
        [ObservableProperty]
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


        public void SetLine(string lineName)
        {
            if (Source == null) { return; }

            Source.PropsSet("device::line-in", lineName);
            Source.ObjectStart(null);
        }

        public Task InitSource(string? lineName)
        {
            Source = new MLiveClass();
            //Source.PropsSet("object::external_process", "false");
            return Task.Run(() =>
            {

                Source.DeviceSet("video", "NDI Receiver", "ndi_auto_connect=false ndi_close_async=true");
                if (!string.IsNullOrEmpty(lineName))
                {
                    SetLine(lineName);
                }



                var sb = new StringBuilder();

                _ndiFinderThread = new Thread(() =>
                {
                    while (runThread)
                    {
                        Source.PropsGet("device::stat::ndi_find_src", out var srcCount);

                        if (int.TryParse(srcCount, out var sourcesFoundCount))
                            SourcesFoundCount = sourcesFoundCount;

                        Source.PropsGet("device::line-in-split", out var lineSplit);
                        if (lineSplit != null)
                        {


                            sb.Clear();
                            sb.AppendLine($"{SourceId} => {srcCount}");
                            var lines = lineSplit.Split('|');

                            var toRemove = Sources.Except(lines).ToList();
                            var toAdd = lines.Except(Sources).ToList();
                            if (App.Current == null)
                                break;
                            App.Current.Dispatcher.BeginInvoke(() =>
                            {
                                foreach (var lineToRemove in toRemove)
                                    Sources.Remove(lineToRemove);
                                foreach (var lineToAdd in toAdd)
                                    Sources.Add(lineToAdd);
                            });

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

                Source?.ObjectClose();
                if (Source != null)
                    Marshal.ReleaseComObject(Source);
            });
        }

    }
}
