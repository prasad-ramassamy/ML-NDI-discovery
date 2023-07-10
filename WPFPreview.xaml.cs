using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using MPLATFORMLib;

namespace NdiMl
{
    /// <summary>
    /// Interaction logic for WPFPreview.xaml
    /// </summary>
    public sealed partial class WPFPreview : UserControl, IDisposable
    {
        #region Private


        private const double MedialookBasePreviewAbsoluteAttenuation = 100;

        private MPreviewClass? _preview;
        private IntPtr _savedPointer;

        private string? _sourceName;

        private bool _isClosingPreview;



        #endregion

        #region Construct

        /// <summary>
        /// Initializes a new instance of the <see cref="WPFPreview"/> class.
        /// </summary>
        public WPFPreview()
        {
            InitializeComponent();
            Unloaded += WPFPreview_Unloaded;


            SizeChanged += WPFPreview_SizeChanged;
        }

        private void WPFPreview_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_preview == null)
                return;

            _preview.PropsSet("wpf_preview.downscale", GetDownscaleFactor());
        }

        private void WPFPreview_Unloaded(object sender, RoutedEventArgs e)
        {

            if (ClearSourceBindingOnUnload)
            {
                BindingOperations.ClearBinding(this, SourceProperty);
            }

            Dispose(true);
        }


        #endregion

        #region Debug Properties



        #endregion

        #region Dependency Properties

        #region Source

        /// <summary>
        /// The <see cref="Source" /> dependency property's name.
        /// </summary>
        public const string SourcePropertyName = "Source";

        /// <summary>
        /// Gets or sets the value of the <see cref="Source" />
        /// property. This is a dependency property.
        /// </summary>
        public Object Source
        {
            get
            {
                return (Object)GetValue(SourceProperty);
            }
            set
            {
                SetValue(SourceProperty, value);
            }
        }

        /// <summary>
        /// Identifies the <see cref="Source" /> dependency property.
        /// </summary>
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            SourcePropertyName,
            typeof(Object),
            typeof(WPFPreview),
            new FrameworkPropertyMetadata(null, OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is WPFPreview control))
                return;

            if (control._preview != null)
            {
                try
                {
                    control.ClosePreview();
                }
                catch (Exception ex)
                {

                }
            }

            if (control.Source != null)
            {
                control.InitPreview();
            }
        }

        #endregion

        #region PreviewSurface

        /// <summary>
        /// The <see cref="PreviewSurface" /> dependency property's name.
        /// </summary>
        public const string PreviewSurfacePropertyName = "PreviewSurface";

        /// <summary>
        /// Gets or sets the value of the <see cref="PreviewSurface" />
        /// property. This is a dependency property.
        /// </summary>
        public D3DImage PreviewSurface
        {
            get
            {
                return (D3DImage)GetValue(PreviewSurfaceProperty);
            }
            set
            {
                SetValue(PreviewSurfaceProperty, value);
            }
        }

        /// <summary>
        /// Identifies the <see cref="PreviewSurface" /> dependency property.
        /// </summary>
        public static readonly DependencyProperty PreviewSurfaceProperty = DependencyProperty.Register(
            PreviewSurfacePropertyName,
            typeof(D3DImage),
            typeof(WPFPreview),
            new UIPropertyMetadata(null));

        #endregion



        #region ClearSourceBindingOnUnload DependencyProperty

        /// <summary>
        /// Gets or sets the value of the 'ClearSourceBindingOnUnload' />
        /// property. This is a dependency property.
        /// </summary>
        public bool ClearSourceBindingOnUnload
        {
            get
            {
                return (bool)GetValue(ClearSourceBindingOnUnloadProperty);
            }
            set
            {
                SetValue(ClearSourceBindingOnUnloadProperty, value);
            }
        }

        /// <summary>
        /// Identifies the <see cref="ClearSourceBindingOnUnload" /> dependency property.
        /// </summary>
        public static readonly DependencyProperty ClearSourceBindingOnUnloadProperty = DependencyProperty.Register(
            nameof(ClearSourceBindingOnUnload),
            typeof(bool),
            typeof(WPFPreview),
            new UIPropertyMetadata(true));

        #endregion



        /// <summary>
        /// Gets or sets the preview.
        /// </summary>
        public MPreviewClass Preview
        {
            get { return (MPreviewClass)GetValue(PreviewProperty); }
            set { SetValue(PreviewProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Preview.  This enables animation, styling, binding, etc...
        /// <summary>
        /// The preview property
        /// </summary>
        public static readonly DependencyProperty PreviewProperty =
            DependencyProperty.Register("Preview", typeof(MPreviewClass), typeof(WPFPreview), new PropertyMetadata(null));

        #endregion

        #region Private methods

        private void InitPreview()
        {
            try
            {
                ((IMObject)Source).ObjectNameGet(out _sourceName);



                PreviewSurface = new D3DImage();

                _preview = new MPreviewClass();
                Preview = _preview;
                // to enable WPF preview through D3D surface we need to set this property:
                _preview.PropsSet("wpf_preview", "true");
                // don't let medialooks to update surface.
                _preview.PropsSet("wpf_preview.update", "0");
                // downscale preview for better performance
                _preview.PropsSet("wpf_preview.downscale", GetDownscaleFactor());

                _preview.ObjectNameSet($"{_sourceName}_PREVIEW");

                _preview.PreviewEnable("", 1, 1);


                _preview.ObjectStart(Source);
                _preview.OnEventSafe += Preview_OnEventSafe;


            }
            catch (InvalidComObjectException comEx)
            {

            }
            catch (Exception ex)
            {

            }
        }


        private void ClosePreview()
        {
            if (_isClosingPreview)
                return;
            _isClosingPreview = true;



            if (PreviewSurface != null)
            {
                PreviewSurface.Lock();
                PreviewSurface.SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
                PreviewSurface.Unlock();

            }

            if (_savedPointer != IntPtr.Zero)
            {
                Marshal.Release(_savedPointer);
                _savedPointer = IntPtr.Zero;
            }

            if (_preview != null)
            {
                try
                {
                    _preview.OnEventSafe -= Preview_OnEventSafe;
                    _preview.ObjectClose();
                    Marshal.ReleaseComObject(_preview);
                    _preview = null;

                }
                catch (AccessViolationException e)
                {

                }
            }
            _isClosingPreview = false;
        }

        private void Preview_OnEventSafe(string bsChannelID, string bsEventName, string bsEventParam, object pEventObject)
        {
            if (bsEventName == "wpf_nextframe")
            {
                if (!PreviewSurface.IsFrontBufferAvailable)
                    return;

                IntPtr pEventObjectPtr = Marshal.GetIUnknownForObject(pEventObject);
                if (pEventObjectPtr != _savedPointer)
                {
                    if (_savedPointer != IntPtr.Zero)
                        Marshal.Release(_savedPointer);

                    _savedPointer = pEventObjectPtr;

                    PreviewSurface.Lock();
                    PreviewSurface.SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
                    PreviewSurface.SetBackBuffer(D3DResourceType.IDirect3DSurface9, _savedPointer, true);
                    PreviewSurface.Unlock();
                }
                else if (pEventObjectPtr != IntPtr.Zero)
                {
                    Marshal.Release(pEventObjectPtr);
                }

                PreviewSurface.Lock();
                PreviewSurface.AddDirtyRect(new Int32Rect(0, 0, PreviewSurface.PixelWidth, PreviewSurface.PixelHeight));
                PreviewSurface.Unlock();

                Marshal.ReleaseComObject(pEventObject);
            }
        }


        private string GetDownscaleFactor()
        {
            if (_preview == null)
                return "0";

            if (ActualWidth <= 0 || Double.IsNaN(ActualWidth))
                return "0";

            var sourceAlias = Source;
            if (sourceAlias == null)
                return "0";
            ((IMFormat)sourceAlias).FormatVideoGet(eMFormatType.eMFT_Output, out M_VID_PROPS vidProps, out _, out _);

            var sourceWitdh = vidProps.nWidth;

            var ratio = sourceWitdh / ActualWidth;
            var factor = Math.Ceiling(Math.Min(ratio, 4)).ToString();



            return factor;
        }

        #endregion

        #region Dispose implementation

        private bool _isDisposed = false;

        /// <summary>
        /// Implement IDisposable. 
        /// </summary>
        /// <remarks>
        /// Do not make this method virtual. 
        /// A derived class should not be able to override this method. 
        /// </remarks>
        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose(bool disposing) executes in two distinct scenarios. 
        /// If disposing equals true, the method has been called directly 
        /// or indirectly by a user's code. Managed and unmanaged resources 
        /// can be disposed. 
        /// If disposing equals false, the method has been called by the 
        /// runtime from inside the finalizer and you should not reference 
        /// other objects. Only unmanaged resources can be disposed. 
        /// </summary>
        private void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called. 
            if (!_isDisposed)
            {
                // If disposing equals true, dispose all managed 
                // and unmanaged resources. 
                if (disposing)
                {
                    ClosePreview();
                }

                // Note disposing has been done.
                _isDisposed = true;
            }
        }

        /// <summary>
        /// Use C# destructor syntax for finalization code. 
        /// </summary>
        /// <remarks>This destructor will run only if the Dispose method 
        /// does not get called. 
        /// It gives your base class the opportunity to finalize. 
        /// Do not provide destructors in types derived from this class.
        /// </remarks>
        ~WPFPreview()
        {
            Dispose(false);
        }

        #endregion
    }
}
