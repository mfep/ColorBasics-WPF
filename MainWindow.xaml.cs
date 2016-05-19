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
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Runtime.InteropServices;
    using Microsoft.Kinect;
    using System.Threading.Tasks;    

    using System.Linq;

    using Emgu.CV;
    using Emgu.CV.Structure;
    using Emgu.CV.CvEnum;
    using Emgu.CV.Util;

    #region fields
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Width of the depth/infrared image in pixels
        /// </summary>
        const int imageSizeX = 512;
        /// <summary>
        /// Height of the depth/infrared image in pixels
        /// </summary>
        const int imageSizeY = 424;

        IntPtr convertedColorDataPtr = IntPtr.Zero;
		IntPtr convertedInfraredDataPtr = IntPtr.Zero;

        /// <summary>
        /// Stores the currently selected HSV color to inspect
        /// </summary>
        byte[] filterHsv = new byte[] { 60, 255, 255 };
        /// <summary>
        /// Pixel position of the point that needs to be inspected in the next frame
        /// </summary>
        System.Drawing.Point? inspectPosition = null;
        /// <summary>
        /// Array holding the data needed for the screen->world transformation
        /// </summary>
        PointF[] cameraSpaceTable = null;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Reader for color frames
        /// </summary>
        /// 
        private MultiSourceFrameReader multiSourceFrameReader = null;
        private ColorFrameReader colorFrameReader = null;
        private InfraredFrameReader infraredFrameReader = null;
        private DepthFrameReader depthFrameReader = null;
        /// <summary>
        /// Bitmap to display
        /// </summary>
        private WriteableBitmap bitmap1 = null;
        private WriteableBitmap bitmap2 = null;
        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;    

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource1
        {
            get
            {
                return this.bitmap1;
            }
        }
        public ImageSource ImageSource2
        {
            get
            {
                return this.bitmap2;
            }
        }
        private string outText = "";
        public string OutText
        {
            get
            {
                return outText;
            }
            set
            {

            }
        }
        double cannyThreshold = 200.0;
        /// <summary>
        /// [GET/SET] The canny threshold used in the EmguCV Canny edge detection algorithm
        /// </summary>
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

        double cannyThresholdLinking = 200.0;        
        /// <summary>
        /// [GET/SET] The canny threshold linking used in the EmguCV Canny edge detection algorithm
        /// </summary>
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

        double infraMultiplier = 3.0;
        /// <summary>
        /// [GET/SET] the multiplier value applied to converted (8bit)
        /// </summary>
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
        #endregion
        #region constructor
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
            this.multiSourceFrameReader = this.kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Infrared | FrameSourceTypes.Depth);

            // wire handler for frame arrival
            this.colorFrameReader.FrameArrived += this.Reader_ColorFrameArrived;
            this.infraredFrameReader.FrameArrived += this.Reader_InfraredFrameArrived;
            this.depthFrameReader.FrameArrived += this.Reader_DepthFrameArrived;
            this.multiSourceFrameReader.MultiSourceFrameArrived += this.Reader_MultiSourceFrameArrived;

            //event handler for the coordinate changed callback
            kinectSensor.CoordinateMapper.CoordinateMappingChanged += CoordinateMappingChangedCallback;
            // create the colorFrameDescription from the ColorFrameSource using Bgra format
            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);

            // create the bitmap to display
            this.bitmap1 = new WriteableBitmap(imageSizeX, imageSizeY, 96.0, 96.0, PixelFormats.Bgr24, null);
            this.bitmap2 = new WriteableBitmap(imageSizeX, imageSizeY, 96.0, 96.0, PixelFormats.Bgr24, null);
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
        #endregion
        #region colorframearrived
        /// <summary>
        /// Handles the color frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private async void Reader_ColorFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            //fast return to check if color frame processing is enabled by the user
            if (!colorRadioButton.IsChecked ?? false) {
                return;
            }            
            using (ColorFrame colorFrame = e.FrameReference.AcquireFrame()) {
                if(colorFrame != null) {
                    //asynchronous call to speed up the execution
                    var processedFrameTuple = await ProcessColorFrame(colorFrame);
                    DisplayMatOnBitmap(processedFrameTuple.Item1, this.bitmap1);
                    //display the canny filtered image
                    //CvInvoke.Imshow("edges", processedFrameTuple.Item2);
                    DisplayMatOnBitmap(processedFrameTuple.Item2, bitmap2);
                    //manually dispose the Mats (identical to using(...){...})
                    processedFrameTuple.Item1.Dispose();
                    processedFrameTuple.Item2.Dispose();
                }
            }
        }
        #endregion
        #region processcolorframe
        /// <summary>
        /// Performs color based threshold on the color frame
        /// </summary>
        /// <param name="colorFrame"></param>
        /// <returns></returns>
        private async Task<Tuple<Mat, Mat>> ProcessColorFrame(ColorFrame colorFrame)
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


                        if (inspectPosition != null) {
                            InspectPixel(resizedImage);
                        }
                    }
                    //return Tuple.Create(resizedImage, CannyShapeDetection(resizedImage));
                    return Tuple.Create(resizedImage, ChromaShapeDetection(resizedImage));
                }
            });
        }
        #endregion
        #region cannyshapedetection
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
        #endregion
        #region chromashapedetection
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
        #endregion
        #region inspectpixel
        private void InspectPixel(Mat mat)
        {
            if (inspectPosition == null)
                return;

            var colorPos = inspectPosition ?? new System.Drawing.Point(0, 0);
            inspectPosition = null;

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
        #endregion
        #region infraredframearrived
        /// <summary>
        /// Performs triangle detection on the infrared stream.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Reader_InfraredFrameArrived(object sender, InfraredFrameArrivedEventArgs e)
        {
            //check if infrared frame processing is enabled by the user
            if(!infraredRadioButton.IsChecked ?? false) {
                return;
            }
            using (InfraredFrame infraredFrame = e.FrameReference.AcquireFrame()) {                
                var infraredFrameDescription = this.infraredFrameReader.InfraredFrameSource.FrameDescription;

				using (Mat infraredMat = new Mat(infraredFrameDescription.Height, infraredFrameDescription.Width, DepthType.Cv16U, 1)) {
                    //convert to Emgu.CV.Mat
					infraredFrame?.CopyFrameDataToIntPtr(infraredMat.DataPointer, infraredFrameDescription.LengthInPixels * 2);
                    //main processing
					TriangleFromInfrared(infraredMat);
                    //display
					CvInvoke.Imshow("infrared", infraredMat);
				}					
            }
        }
        #endregion
        #region trianglefrominfrared
        private Triangle2DF TriangleFromInfrared(Mat infraredMat)
        {
            Image<Gray, short> infraredImg = infraredMat.ToImage<Gray, short>();
            var smoothedInfraredImg = infraredImg.PyrDown();
            smoothedInfraredImg = smoothedInfraredImg.PyrUp();

            using (Mat convertedMat = new Mat(infraredMat.Size, DepthType.Cv8U, 1))
            using (Mat multiplierMat = new Mat(infraredMat.Size, DepthType.Cv8U, 1)) {
                infraredImg.Mat.ConvertTo(convertedMat, DepthType.Cv8U, 1d / 256d);

                multiplierMat.SetTo(new MCvScalar(infraMultiplier));

                CvInvoke.Multiply(convertedMat, multiplierMat, convertedMat);

                Mat thresholdMat = new Mat(infraredMat.Size, DepthType.Cv8U, 1);
                CvInvoke.Threshold(convertedMat, thresholdMat, 230, 255, ThresholdType.Binary);

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

                foreach (var triangle in triangleList) {
                    CvInvoke.Polylines(convertedMat, Array.ConvertAll(triangle.GetVertices(), System.Drawing.Point.Round), true, new MCvScalar(255));
                }
                //CvInvoke.Imshow("threshold", thresholdMat);

                return biggestTri;

            }
        }
        #endregion
        #region depthframearrived
        private void Reader_DepthFrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            //check if depth frame processing is enabled
            if(!depthRadioButton.IsChecked ?? false)
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
        #endregion
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
        #region displaymatonbitmap
        private void DisplayMatOnBitmap (Mat mat, WriteableBitmap bitmap)
        {
            bitmap.Lock();

            bitmap.WritePixels(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight), mat.DataPointer, bitmap.PixelWidth * bitmap.PixelHeight * 3, mat.Step);
            bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));

            bitmap.Unlock();
        }
        #endregion
        private void Image_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(this.image1);
            inspectPosition = new System.Drawing.Point((int)Math.Round(pos.X), (int)Math.Round(pos.Y));                        
        }
        #region multisourceframearrived
        private void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            if (!multiRadioButton.IsChecked ?? false)
                return;

            var multiFrame = e.FrameReference.AcquireFrame();

            using (var infraredFrame = multiFrame.InfraredFrameReference.AcquireFrame())
            using (var depthFrame = multiFrame.DepthFrameReference.AcquireFrame())
            {
                if (infraredFrame == null || depthFrame == null)
                    return;

                var frameSize = new System.Drawing.Size(infraredFrame.FrameDescription.Width, infraredFrame.FrameDescription.Height);
                var frameDataLength = infraredFrame.FrameDescription.BytesPerPixel * infraredFrame.FrameDescription.LengthInPixels;

                using (Mat triMaskMat = new Mat(frameSize, DepthType.Cv8U, 1))
                using (Mat infraredMat = new Mat(frameSize, DepthType.Cv16U, 1))
                using (Mat maskedInfraredMat = new Mat(frameSize, DepthType.Cv16U, 1))
                using (Mat depthMat = new Mat(frameSize, DepthType.Cv16U, 1)) {
                    infraredFrame.CopyFrameDataToIntPtr(infraredMat.DataPointer, frameDataLength);
                    depthFrame.CopyFrameDataToIntPtr(depthMat.DataPointer, frameDataLength);
                    triMaskMat.SetTo(new MCvScalar(0));
                    maskedInfraredMat.SetTo(new MCvScalar(0));

                    var tri = TriangleFromInfrared(infraredMat);

                    DisplayDepthHSV(depthMat);
                    //CalculateNormal(depthMat, tri);
                    //CvInvoke.Imshow("triangle", DisplayTriangleOnInfrared(infraredMat, tri));
                    DisplayMatOnBitmap(DisplayTriangleOnInfrared(infraredMat, tri), bitmap2);
                }
            }
        }
        #endregion
        private void CalculateNormal(Mat depthMat, Triangle2DF tri)
        {
            if (depthMat == null || (tri.Centeroid.X == 0 && tri.Centeroid.Y == 0))
                return;

            var vertices = Array.ConvertAll(tri.GetVertices(), System.Drawing.Point.Round);
            var point2dList = vertices;// Kolos.haromszog.belsopontok(vertices);

            #region Kolos normalvektor
            //var point3dList = new List<Kolos.pont3d>();

            //foreach (var point2d in point2dList) {
            //    var zcoord = GetMatElementU16(depthMat, point2d.X, point2d.Y);
            //    if(zcoord != 0)
            //        point3dList.Add(CalculateWorldPosition(point2d.X, point2d.Y, depthMat.Cols, zcoord));
            //}

            //var normal = Kolos.normalvektor.kiszamitas(point3dList);
            //Console.WriteLine($"Normálvektor X:{normal[0].ToString("F4")} Y:{normal[1].ToString("F4")}, Z: {normal[2].ToString("F4")}");
            #endregion


            MCvPoint3D32f[] vertices3d = new MCvPoint3D32f[3];
            for (int i = 0; i < 3; i++) {
                var point2d = point2dList[i];
                var zcoord = GetMatElementU16(depthMat, point2d.X, point2d.Y);
                if (zcoord == 0)
                    return;
                var point3d = CalculateWorldPosition(point2d.X, point2d.Y, depthMat.Cols, zcoord);
                vertices3d[i] = new MCvPoint3D32f((float)point3d.x, (float)point3d.y, (float)point3d.z);
            }
            
            var tri3d = new Triangle3DF(vertices3d[0], vertices3d[1], vertices3d[2]);
            var normal = tri3d.Normal;
            Console.WriteLine($"Normal: X:{normal.X} Y:{normal.Y} Z:{normal.Z}");
        }

        private Mat DisplayTriangleOnInfrared(Mat infraredMat, Triangle2DF tri)
        {
            using (Mat convertMat = new Mat(infraredMat.Size, DepthType.Cv8U, 1)) {
                infraredMat.ConvertTo(convertMat, DepthType.Cv8U, 1 / 256d);
                Mat displayMat = new Mat(infraredMat.Size, DepthType.Cv8U, 3);
                CvInvoke.CvtColor(convertMat, displayMat, ColorConversion.Gray2Bgr);
                CvInvoke.Polylines(displayMat, Array.ConvertAll(tri.GetVertices(), System.Drawing.Point.Round), true, new Bgr(0, 0, 255).MCvScalar, 2);
                return displayMat;
            }
        }
        #region displaydepthhsv
        private void DisplayDepthHSV(Mat depthMat16U)
        {
            if (depthMat16U == null)
                return;

            using (Mat convertedMat8U = new Mat(depthMat16U.Size, DepthType.Cv8U, 1))
            using (Mat colorMat8U3 = new Mat(depthMat16U.Size, DepthType.Cv8U, 3))
            using (Mat hsvMat8U3 = new Mat(depthMat16U.Size, DepthType.Cv8U, 3))
            using (Mat hsvConstantMat = new Mat(depthMat16U.Size, DepthType.Cv8U, 3))
            {
                depthMat16U.ConvertTo(convertedMat8U, DepthType.Cv8U, 1 / 256d);
                CvInvoke.CvtColor(convertedMat8U, colorMat8U3, ColorConversion.Gray2Bgr);

                hsvConstantMat.SetTo(new MCvScalar(0, 255, 255));
                CvInvoke.BitwiseOr(colorMat8U3, hsvConstantMat, hsvMat8U3);

                CvInvoke.CvtColor(hsvMat8U3, hsvMat8U3, ColorConversion.Hsv2Bgr);                

                DisplayMatOnBitmap(hsvMat8U3, this.bitmap1);
                InspectDepthPixel(depthMat16U);
            }
        }
        #endregion
        #region worldposition
        private void InspectDepthPixel(Mat depthMat16U)
        {
            if (inspectPosition == null || depthMat16U == null)
                return;

            var pos = inspectPosition.Value;
            if (pos.X >= depthMat16U.Cols || pos.Y >= depthMat16U.Rows)
                return;

            Console.WriteLine($"Depth value at X:{pos.X} Y:{pos.Y} is {GetMatElementU16(depthMat16U, pos.X, pos.Y)}");
            outText += $"Depth value at X:{pos.X} Y:{pos.Y} is {GetMatElementU16(depthMat16U, pos.X, pos.Y)}" + Environment.NewLine;
            var worldPos = CalculateWorldPosition(pos.X, pos.Y, depthMat16U.Cols, GetMatElementU16(depthMat16U, pos.X, pos.Y));
            Console.WriteLine($"Clicked World pos: X:{worldPos.x} Y:{worldPos.y} Z:{worldPos.z}");
            outText += $"Clicked World pos: X:{worldPos.x.ToString("F3")} Y:{worldPos.y.ToString("F3")} Z:{worldPos.z}" + Environment.NewLine;
            textBox.Text = outText;
            inspectPosition = null;
        }
        private unsafe ushort GetMatElementU16(Mat mat, int x, int y)
        {
            ushort* dataPtr = (ushort*)mat.DataPointer;
            return *(dataPtr + x + mat.Cols * y);
        }
        private MathUtil.Pont3d CalculateWorldPosition(int screenX, int screenY, int width, ushort depthValue)
        {
            PointF lookupValue = cameraSpaceTable[screenX + screenY * width];
            return new MathUtil.Pont3d(lookupValue.X * depthValue, lookupValue.Y * depthValue, depthValue);
        }
        private void CoordinateMappingChangedCallback(object sender, CoordinateMappingChangedEventArgs args)
        {
            cameraSpaceTable = kinectSensor.CoordinateMapper.GetDepthFrameToCameraSpaceTable();
        }
        #endregion
        private void CloseCVWindows()
        {
            CvInvoke.DestroyAllWindows();
        }

        private void colorRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            CloseCVWindows();
        }

        private void infraredRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            CloseCVWindows();
        }

        private void depthRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            CloseCVWindows();
        }
    }
}
