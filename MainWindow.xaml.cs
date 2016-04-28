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

    using System.Linq;

    using Emgu.CV;
    using Emgu.CV.Structure;
    using Emgu.CV.CvEnum;
    using Emgu.CV.Util;
    using Emgu.CV.UI;

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        const int imageSizeX = 480;
        const int imageSizeY = 270;
        IntPtr convertedColorDataPtr = IntPtr.Zero;
		IntPtr convertedInfraredDataPtr = IntPtr.Zero;
        double cannyThreshold = 200.0;
        double cannyThresholdLinking = 200.0;
        double infraMultiplier = 3.0;
        System.Drawing.Point? inspectColorPosition = null;
        byte[] filterHsv = new byte[] { 60, 255, 255 };
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Reader for color frames
        /// </summary>
        private ColorFrameReader colorFrameReader = null;
        private InfraredFrameReader infraredFrameReader = null;
        private DepthFrameReader depthFrameReader = null;

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
            this.infraredFrameReader = this.kinectSensor.InfraredFrameSource.OpenReader();
            this.depthFrameReader = this.kinectSensor.DepthFrameSource.OpenReader();

            // wire handler for frame arrival
            this.colorFrameReader.FrameArrived += this.Reader_ColorFrameArrived;
            this.infraredFrameReader.FrameArrived += this.Reader_InfraredFrameArrived;
            this.depthFrameReader.FrameArrived += this.Reader_DepthFrameArrived;

            // create the colorFrameDescription from the ColorFrameSource using Bgra format
            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);

            // create the bitmap to display
            this.colorBitmap = new WriteableBitmap(imageSizeX, imageSizeY, 96.0, 96.0, PixelFormats.Bgr24, null);
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
        public double InfraMultiplier
        {
            get
            {
                return infraMultiplier;
            }
            set
            {
                infraMultiplier = value;
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

            if (convertedColorDataPtr != IntPtr.Zero) {
                Marshal.FreeHGlobal(convertedColorDataPtr);
            }
        }

        /// <summary>
        /// Handles the color frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private async void Reader_ColorFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            if (!colorCheckBox.IsChecked ?? false) {
                return;
            }
            // ColorFrame is IDisposable
            using (ColorFrame colorFrame = e.FrameReference.AcquireFrame()) {
                if(colorFrame != null) {
                    var processedFrameTuple = await ProcessColorFrame(colorFrame);
                    //CvInvoke.Imshow("original", processedFrameTuple.Item1);
                    DisplayMatOnBitmap(processedFrameTuple.Item1, this.colorBitmap);
                    CvInvoke.Imshow("edges", processedFrameTuple.Item2);
                    processedFrameTuple.Item1.Dispose();
                    processedFrameTuple.Item2.Dispose();
                }
            }
        }

        private void Reader_InfraredFrameArrived(object sender, InfraredFrameArrivedEventArgs e)
        {
            if(!infraredCheckBox.IsChecked ?? false) {
                return;
            }
            using (InfraredFrame infraredFrame = e.FrameReference.AcquireFrame()) {
                var infraredFrameDescription = this.infraredFrameReader.InfraredFrameSource.FrameDescription;

				using (Mat infraredMat = new Mat(infraredFrameDescription.Height, infraredFrameDescription.Width, DepthType.Cv16U, 1)) {

					infraredFrame?.CopyFrameDataToIntPtr(infraredMat.DataPointer, infraredFrameDescription.LengthInPixels * 2);

					ProcessInfraredMat(infraredMat);

					CvInvoke.Imshow("infrared", infraredMat);
				}					
            }
        }

        private void Reader_DepthFrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            if(!depthCheckBox.IsChecked ?? false)
                return;                        

            using(DepthFrame depthFrame = e.FrameReference.AcquireFrame()) {

                if (depthFrame == null)
                    return;

                FrameDescription depthFrameDescription = depthFrame.FrameDescription;

                using (Mat depthMat = new Mat(depthFrameDescription.Height, depthFrameDescription.Width, DepthType.Cv16U, 1))
                using (Mat convertedMat = new Mat(depthFrameDescription.Height, depthFrameDescription.Width, DepthType.Cv8U, 1))
                {

                    depthFrame.CopyFrameDataToIntPtr(depthMat.DataPointer, depthFrameDescription.BytesPerPixel * depthFrameDescription.LengthInPixels);
                    depthMat.ConvertTo(convertedMat, DepthType.Cv8U, 1 / 256d);
                    CvInvoke.Imshow("depth", convertedMat);
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
				
		private void ProcessInfraredMat(Mat infraredMat)
		{
			Image<Gray, short> infraredImg = infraredMat.ToImage<Gray, short>();
			var smoothedInfraredImg = infraredImg.PyrDown();
			smoothedInfraredImg = smoothedInfraredImg.PyrUp();					

			//using (Mat smoothedMat = smoot)
			using (Mat convertedMat = new Mat(infraredMat.Size, DepthType.Cv8U, 1))
			//using (Mat cannyMat = new Mat(infraredMat.Size, DepthType.Cv8U, 1))
            {
				infraredImg.Mat.ConvertTo(convertedMat, DepthType.Cv8U, 1d / 256d);

                Mat multiplierMat = new Mat(infraredMat.Size, DepthType.Cv8U, 1);
                multiplierMat.SetTo(new MCvScalar(infraMultiplier));

                CvInvoke.Multiply(convertedMat, multiplierMat, convertedMat);            
                CvInvoke.Imshow("infrared -> converted", convertedMat);

                Mat thresholdMat = new Mat(infraredMat.Size, DepthType.Cv8U, 1);
                CvInvoke.Threshold(convertedMat, thresholdMat, 230, 255, ThresholdType.Binary);
                CvInvoke.Imshow("infrared -> converted -> threshold", thresholdMat);

                //CvInvoke.Canny(thresholdMat, cannyMat, cannyThreshold, cannyThresholdLinking);

                #region triangles
                List<Triangle2DF> triangleList = new List<Triangle2DF>();

                using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint()) {
                    CvInvoke.FindContours(thresholdMat, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);
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

                var biggestTri = triangleList.OrderBy((tri) => tri.Area).FirstOrDefault();                
                CvInvoke.Polylines(thresholdMat, Array.ConvertAll(biggestTri.GetVertices(), System.Drawing.Point.Round), true, new MCvScalar(255));

                //foreach (var triangle in triangleList) {
                //    CvInvoke.Polylines(cannyMat, Array.ConvertAll(triangle.GetVertices(), System.Drawing.Point.Round), true, new MCvScalar(255));
                //}

                CvInvoke.Imshow("infrared -> converted -> canny(tris)", thresholdMat);
                #endregion
            }
        }

		private async Task<Tuple<Mat, Mat>> ProcessColorFrame (ColorFrame colorFrame)
        {
            return await Task.Run(() =>
            {
                FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                using (KinectBuffer colorBuffer = colorFrame.LockRawImageBuffer()) {

                    if (convertedColorDataPtr == IntPtr.Zero) {
                        convertedColorDataPtr = Marshal.AllocHGlobal(4 * (int)colorFrameDescription.LengthInPixels);
                    }

                    colorFrame.CopyConvertedFrameDataToIntPtr(convertedColorDataPtr, 4 * colorFrameDescription.LengthInPixels, ColorImageFormat.Bgra);

                    Mat resizedImage = new Mat();
                    using (Mat convertedImage_Bgr = new Mat(colorFrameDescription.Height, colorFrameDescription.Width, DepthType.Cv8U, 3))
                    using (Mat convertedImage_Bgra = new Mat(colorFrameDescription.Height, colorFrameDescription.Width, DepthType.Cv8U, 4, convertedColorDataPtr, colorFrameDescription.Width * 4)) {

                        CvInvoke.CvtColor(convertedImage_Bgra, convertedImage_Bgr, ColorConversion.Bgra2Bgr);
                        CvInvoke.Resize(convertedImage_Bgr, resizedImage, new System.Drawing.Size(imageSizeX, imageSizeY));


                        if(inspectColorPosition != null) {
                            InspectPixel(resizedImage);
                        }
                    }
                    //return Tuple.Create(resizedImage, CannyShapeDetection(resizedImage));
                    return Tuple.Create(resizedImage, ChromaShapeDetection(resizedImage));
                }
            });            
        }

        private Mat CannyShapeDetection(Mat frame)
        {
            Mat returnImg = new Mat(frame.Rows, frame.Cols, frame.Depth, frame.NumberOfChannels);
            CvInvoke.Canny(frame, returnImg, cannyThreshold, cannyThresholdLinking);                                    

            List<Triangle2DF> triangleList = new List<Triangle2DF>();

            using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint()) {
                CvInvoke.FindContours(returnImg, contours, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);
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
                CvInvoke.Polylines(returnImg, Array.ConvertAll(triangle.GetVertices(), System.Drawing.Point.Round), true, new MCvScalar(255));
            }
            
            return returnImg;
        }

        private Mat ChromaShapeDetection(Mat frame)
        {
            Mat chromaFrame = new Mat(frame.Size, frame.Depth, frame.NumberOfChannels);

            MCvScalar lowerLimit = new MCvScalar((filterHsv[0] - 5) % 180, 0, 0);//(filterHsv[1] - 100) % 255, (filterHsv[2] - 100) % 255);
            MCvScalar upperLimit = new MCvScalar((filterHsv[0] + 5) % 180, 255, 255);//(filterHsv[1] + 100) % 255, (filterHsv[2] + 100) % 255);

            using (Mat lowerLimits = new Mat(frame.Size, frame.Depth, frame.NumberOfChannels))
            using (Mat upperLimits = new Mat(frame.Size, frame.Depth, frame.NumberOfChannels))
            using (Mat hsvFrame = new Mat()) {
                CvInvoke.CvtColor(frame, hsvFrame, ColorConversion.Bgr2Hsv);
                lowerLimits.SetTo(lowerLimit);
                upperLimits.SetTo(upperLimit);
                CvInvoke.InRange(hsvFrame, lowerLimits, upperLimits, chromaFrame);
                CvInvoke.MedianBlur(chromaFrame, chromaFrame, 7);                
            }

            return chromaFrame;   
        }

        private void InspectPixel(Mat mat)
        {
            if (inspectColorPosition == null)
                return;

            var colorPos = inspectColorPosition ?? new System.Drawing.Point(0, 0);
            inspectColorPosition = null;

            var image = mat.ToImage<Bgr, byte>();

            var filterBgr = new byte[] { image.Data[colorPos.Y, colorPos.X, 0], image.Data[colorPos.Y, colorPos.X, 1], image.Data[colorPos.Y, colorPos.X, 2] };       

            Mat input = new Mat(1, 1, DepthType.Cv8U, 3);
            Mat output = new Mat(1, 1, DepthType.Cv8U, 3);
            input.SetTo(filterBgr);

            CvInvoke.CvtColor(input, output, ColorConversion.Bgr2Hsv);

            filterHsv = output.GetData();
            Console.WriteLine($"R: {filterBgr[2]}, G: {filterBgr[1]}, B: {filterBgr[0]}");
            Console.WriteLine($"H: {filterHsv[0]}, S: {filterHsv[1]}, V: {filterHsv[2]}");
        }

        private void DisplayMatOnBitmap (Mat mat, WriteableBitmap bitmap)
        {
            bitmap.Lock();

            colorBitmap.WritePixels(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight), mat.DataPointer, bitmap.PixelWidth * bitmap.PixelHeight * 3, mat.Step);
            colorBitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));

            bitmap.Unlock();
        }

        private void Image_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(this.colorImage);
            inspectColorPosition = new System.Drawing.Point((int)Math.Round(pos.X), (int)Math.Round(pos.Y));                        
        }
    }
}
