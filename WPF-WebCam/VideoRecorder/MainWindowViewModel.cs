using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Accord.Video.FFMPEG;
using AForge.Video;
using AForge.Video.DirectShow;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using Microsoft.Win32;

namespace VideoRecorder
{
    internal class MainWindowViewModel : ObservableObject, IDisposable
    {
        #region Private fields

        private FilterInfo _currentDevice;

        private BitmapImage _image;

        private bool _isWebcamSource;

        private IVideoSource _videoSource;
        private VideoFileWriter _writer;
        private bool _recording;
        private DateTime? _firstFrameTime;

        #endregion

        #region Constructor

        public MainWindowViewModel()
        {
            VideoDevices = new ObservableCollection<FilterInfo>();
            GetVideoDevices();
            StartSourceCommand = new RelayCommand(StartCamera);
            StopSourceCommand = new RelayCommand(StopCamera);
            StartRecordingCommand = new RelayCommand(StartRecording);
            StopRecordingCommand = new RelayCommand(StopRecording);
            SaveSnapshotCommand = new RelayCommand(SaveSnapshot);
        }

        #endregion

        #region Properties

        public ObservableCollection<FilterInfo> VideoDevices { get; set; }

        public BitmapImage Image
        {
            get { return _image; }
            set { Set(ref _image, value); }
        }

        public bool IsWebcamSource
        {
            get { return _isWebcamSource; }
            set { Set(ref _isWebcamSource, value); }
        }

        public FilterInfo CurrentDevice
        {
            get { return _currentDevice; }
            set { Set(ref _currentDevice, value); }
        }

        public ICommand StartRecordingCommand { get; private set; }

        public ICommand StopRecordingCommand { get; private set; }

        public ICommand StartSourceCommand { get; private set; }

        public ICommand StopSourceCommand { get; private set; }

        public ICommand SaveSnapshotCommand { get; private set; }

        #endregion

        /// <summary>
        /// <seealso cref="GetVideoDevices"/> sınıfı bilgisayar üzerinde aktif çalışan kamera cihazını buluyor
        /// eğer kamera bulunamazsa bunu kullanıcıya iletiyor
        /// </summary>
        private void GetVideoDevices()
        {
            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo device in devices)
            {
                VideoDevices.Add(device);
            }
            if (VideoDevices.Any())
            {
                CurrentDevice = VideoDevices[0];
            }
            else
            {
                MessageBox.Show("WebCam Bulunamadı");
            }
        }
        /// <summary>
        /// <seealso cref="StartCamera"/> sınıfı cihazımızda bulduğumuz kameranın açılmasını sağlıyor
        /// </summary>
        private void StartCamera()
        {
                if (CurrentDevice != null)
                {
                    _videoSource = new VideoCaptureDevice(CurrentDevice.MonikerString);
                    _videoSource.NewFrame += Video_NewFrame;
                    _videoSource.Start();
                }
        }
        /// <summary>
        /// <seealso cref="Video_NewFrame(object, NewFrameEventArgs)"/> sınıfı ile <seealso cref="BitmapHelpers"/> sınıfını kullanarak video kayıt işlemi yapılıyor
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private void Video_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                if (_recording)
                {
                    using (var bitmap = (Bitmap) eventArgs.Frame.Clone())
                    {
                        if (_firstFrameTime != null)
                        {
                            _writer.WriteVideoFrame(bitmap, DateTime.Now - _firstFrameTime.Value);
                        }
                        else
                        {
                            _writer.WriteVideoFrame(bitmap);
                            _firstFrameTime = DateTime.Now;
                        }
                    }
                }
                using (var bitmap = (Bitmap) eventArgs.Frame.Clone())
                {
                    var bi = bitmap.ToBitmapImage();
                    bi.Freeze();
                    Dispatcher.CurrentDispatcher.Invoke(() => Image = bi);
                }
            }
            catch (Exception)
            {
            }
        }
        /// <summary>
        /// <seealso cref="StopCamera"/> sınıfı cihazımızda bulduğumuz kameranın kapatılmasını sağlıyor
        /// </summary>
        private void StopCamera()
        {
            if (_videoSource != null && _videoSource.IsRunning)
            {
                _videoSource.SignalToStop();
                _videoSource.NewFrame -= Video_NewFrame;
            }
            Image = null;
        }
        /// <summary>
        /// <seealso cref="StopRecording"/> sınıfı başlamış olan video kaydını durdurma işlemini yapıyor
        /// </summary>
        private void StopRecording()
        {
            _recording = false;
            _writer.Close();
            _writer.Dispose();
        }
        /// <summary>
        /// <seealso cref="StartRecording"/> sınıfı video kaydını başlatmamıza ve de kaydetme işlemlerini yapıyor
        /// </summary>
        private void StartRecording()
        {
            var dialog = new SaveFileDialog
            {
                FileName = "Video1",
                DefaultExt = ".avi",
                AddExtension = true
            };
            var dialogresult = dialog.ShowDialog();
            if (dialogresult != true)
            {
                return;
            }
            _firstFrameTime = null;
            _writer = new VideoFileWriter();
            _writer.Open(dialog.FileName, (int)Math.Round(Image.Width, 0), (int)Math.Round(Image.Height, 0));
            _recording = true;
        }
        /// <summary>
        /// <seealso cref="SaveSnapshot"/> sınıfı fotoğraf çekmemizi ve çektiğimiz fotoğrafı kaydetme işlemlerini yapıyor
        /// </summary>
        private void SaveSnapshot()
        {
            var dialog = new SaveFileDialog
            {
                FileName = "Resim1",
                DefaultExt = ".png"
            };
            var dialogresult = dialog.ShowDialog();
            if (dialogresult != true)
            {
                return;
            }
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(Image));
            using (var filestream = new FileStream(dialog.FileName, FileMode.Create))
            {
                encoder.Save(filestream);
            }
        }
        public void Dispose()
        {
            if (_videoSource != null && _videoSource.IsRunning)
            {
                _videoSource.SignalToStop();
            }
            _writer?.Dispose();
        }
    }
}
