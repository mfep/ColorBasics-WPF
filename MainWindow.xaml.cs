//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
namespace Microsoft.Samples.Kinect.ColorBasics
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Runtime.InteropServices;
    using Microsoft.Kinect;
    using System.Threading.Tasks;
    using System.Drawing;

    using Emgu.CV;
    using Emgu.CV.Structure;
    using Emgu.CV.CvEnum;
    using Emgu.CV.Util;

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        IntPtr convertedColorData = IntPtr.Zero;
        double cannyThreshold = 200.0;
        double cannyThresholdLinking = 200.0;
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Reader for color frames
        /// </summary>
        private ColorFrameReader colorFrameReader = null;

        /// <summary>
        /// Bitmap to display
        /// </summary>
        private WriteableBitmap colorBitmap = null;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // get the kinectSensor object
            this.kinectSensor = KinectSensor.GetDefault();

            // open the reader for the color frames
            this.colorFrameReader = this.kinectSensor.ColorFrameSource.OpenReader();

            // wire handler for frame arrival
            this.colorFrameReader.FrameArrived += this.Reader_ColorFrameArrived;

            // create the colorFrameDescription from the ColorFrameSource using Bgra format
            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);

            // create the bitmap to display
            this.colorBitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);
            //this.colorBitmap = new WriteableBitmap(colorFrameDescription.Width / 2, colorFrameDescription.Height / 2, 96.0, 96.0, PixelFormats.Bgr32, null);
            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();

            CannyThreshold = 100;
            CannyThresholdLinking = 100;
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.colorBitmap;
            }
        }

        public double CannyThreshold
        {
            get
            {
                return this.cannyThreshold;
            }
            set
            {
                this.cannyThreshold = value;
                this.cannyThresholdTextBox.Text = value.ToString();
            }
        }
        public double CannyThresholdLinking
        {
            get
            {
                return this.cannyThresholdLinking;
            }
            set
            {
                this.cannyThresholdLinking = value;
                this.cannyThresholdLinkingTextBox.Text = cannyThresholdLinking.ToString();
            }
        }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value) {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null) {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.colorFrameReader != null) {
                // ColorFrameReder is IDisposable
                this.colorFrameReader.Dispose();
                this.colorFrameReader = null;
            }

            if (this.kinectSensor != null) {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }

            if (convertedColorData != IntPtr.Zero) {
                Marshal.FreeHGlobal(convertedColorData);
            }
        }

        /// <summary>
        /// Handles the color frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private async void Reader_ColorFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            // ColorFrame is IDisposable
            using (ColorFrame colorFrame = e.FrameReference.AcquireFrame()) {
                if(colorFrame != null) {
                    using(Mat processedFrame = await ProcessColorFrame(colorFrame)) {
                        CvInvoke.Imshow("edges", processedFrame);
                    }
                }
            }
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }                    

        private async Task<Mat> ProcessColorFrame (ColorFrame colorFrame)
        {
            return await Task.Run(() =>
            {
                FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                using (KinectBuffer colorBuffer = colorFrame.LockRawImageBuffer()) {

                    if (convertedColorData == IntPtr.Zero) {
                        convertedColorData = Marshal.AllocHGlobal(4 * (int)colorFrameDescription.LengthInPixels);
                    }

                    colorFrame.CopyConvertedFrameDataToIntPtr(convertedColorData, 4 * colorFrameDescription.LengthInPixels, ColorImageFormat.Bgra);

                    Mat resizedImage = new Mat();

                    using (Mat convertedImage = new Mat(colorFrameDescription.Height, colorFrameDescription.Width, DepthType.Cv8U, 4, convertedColorData, colorFrameDescription.Width * 4)) {
                        CvInvoke.Resize(convertedImage, resizedImage, new System.Drawing.Size(480, 270));
                    }

                    return ShapeDetection(resizedImage);
                }
            });            
        }

        private Mat ShapeDetection(Mat frame)
        {
            Mat cannyEdges = new Mat();
            CvInvoke.Canny(frame, cannyEdges, cannyThreshold, cannyThresholdLinking);

            List<Triangle2DF> triangleList = new List<Triangle2DF>();

            using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint()) {
                CvInvoke.FindContours(cannyEdges, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);
                int count = contours.Size;
                for (int i = 0; i < count; i++) {
                    using (VectorOfPoint contour = contours[i])
                    using (VectorOfPoint approxContour = new VectorOfPoint()) {
                        CvInvoke.ApproxPolyDP(contour, approxContour, CvInvoke.ArcLength(contour, true) * 0.05, true);
                        if (CvInvoke.ContourArea(approxContour, false) > 250) {
                            if (approxContour.Size == 3) {
                                var pts = approxContour.ToArray();
                                triangleList.Add(new Triangle2DF(pts[0], pts[1], pts[2]));
                            }
                        }
                    }
                }
            }
            foreach (var triangle in triangleList) {
                CvInvoke.Polylines(cannyEdges, Array.ConvertAll(triangle.GetVertices(), System.Drawing.Point.Round), true, new MCvScalar(255));
            }
            return cannyEdges;
        }
    }
}
