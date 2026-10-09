using System;
using System.Management;
using NAudio.CoreAudioApi;
using OpenCvSharp;

namespace GestureControlApp
{
    class Program
    {
        // Windows Master Volume Device
        private static MMDevice defaultAudioDevice = new MMDeviceEnumerator()
            .GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        static void Main(string[] args)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("   Gesture-Controlled Volume & Brightness App     ");
            Console.WriteLine("==================================================");
            Console.WriteLine("Hold two bright green markers/objects in camera view.");
            Console.WriteLine("Press 'Q' or 'ESC' on the camera window to exit.\n");

            // Open default webcam
            using VideoCapture capture = new VideoCapture(0);
            if (!capture.IsOpened())
            {
                Console.WriteLine("Error: Unable to access the webcam.");
                return;
            }

            capture.FrameWidth = 640;
            capture.FrameHeight = 480;

            using Mat frame = new Mat();
            using Mat hsvFrame = new Mat();

            int currentVolumePct = (int)(defaultAudioDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
            int currentBrightnessPct = GetBrightness();

            while (true)
            {
                capture.Read(frame);
                if (frame.Empty()) break;

                // Flip horizontally for a mirror effect
                Cv2.Flip(frame, frame, FlipMode.Y);

                // Convert BGR frame to HSV for color tracking
                Cv2.CvtColor(frame, hsvFrame, ColorConversionCodes.BGR2HSV);

                // Define HSV range for bright green tracking markers
                Scalar lowerGreen = new Scalar(35, 100, 100);
                Scalar upperGreen = new Scalar(85, 255, 255);

                using Mat mask = new Mat();
                // Corrected OpenCvSharp InRange call syntax:
                Cv2.InRange(hsvFrame, lowerGreen, upperGreen, mask);

                // Find contours in the frame
                Cv2.FindContours(mask, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                if (contours.Length >= 2)
                {
                    // Sort contours by area descending to find the 2 largest detected points
                    Array.Sort(contours, (c1, c2) => Cv2.ContourArea(c2).CompareTo(Cv2.ContourArea(c1)));

                    Point p1 = GetContourCenter(contours[0]);
                    Point p2 = GetContourCenter(contours[1]);

                    // Draw circles and line connecting the points
                    Cv2.Circle(frame, p1, 8, Scalar.FromRgb(0, 255, 0), -1);
                    Cv2.Circle(frame, p2, 8, Scalar.FromRgb(0, 255, 0), -1);
                    Cv2.Line(frame, p1, p2, Scalar.FromRgb(255, 0, 0), 2);

                    // Calculate distance: sqrt((x2 - x1)^2 + (y2 - y1)^2)
                    double distance = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

                    // Map pixel distance (20px - 200px) to volume percentage (0% - 100%)
                    currentVolumePct = (int)MapRange(distance, 20, 200, 0, 100);
                    SetVolume(currentVolumePct);

                    Cv2.PutText(frame, $"Distance: {(int)distance}px", new Point(20, 30),
                        HersheyFonts.HersheySimplex, 0.6, Scalar.FromRgb(255, 255, 255), 2);
                }

                // Draw UI overlay bar on screen
                DrawUIOverlay(frame, currentVolumePct, currentBrightnessPct);

                // Show camera window
                Cv2.ImShow("C# Gesture Controller", frame);

                int key = Cv2.WaitKey(1);
                if (key == 'q' || key == 'Q' || key == 27) break;
            }

            Cv2.DestroyAllWindows();
        }

        private static Point GetContourCenter(Point[] contour)
        {
            Moments m = Cv2.Moments(contour);
            int cx = (int)(m.M10 / m.M00);
            int cy = (int)(m.M01 / m.M00);
            return new Point(cx, cy);
        }

        private static double MapRange(double val, double inMin, double inMax, double outMin, double outMax)
        {
            double clamped = Math.Max(inMin, Math.Min(inMax, val));
            return outMin + (clamped - inMin) * (outMax - outMin) / (inMax - inMin);
        }

        private static void SetVolume(int percentage)
        {
            float volumeScalar = Math.Max(0.0f, Math.Min(1.0f, percentage / 100.0f));
            defaultAudioDevice.AudioEndpointVolume.MasterVolumeLevelScalar = volumeScalar;
        }

        private static int GetBrightness()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightness");
                foreach (ManagementObject obj in searcher.Get())
                {
                    return Convert.ToInt32(obj["CurrentBrightness"]);
                }
            }
            catch { }
            return 50;
        }

        private static void DrawUIOverlay(Mat frame, int volumePct, int brightnessPct)
        {
            int barHeight = (int)MapRange(volumePct, 0, 100, 400, 150);
            Cv2.Rectangle(frame, new Point(50, 150), new Point(85, 400), Scalar.FromRgb(200, 200, 200), 2);
            Cv2.Rectangle(frame, new Point(50, barHeight), new Point(85, 400), Scalar.FromRgb(0, 255, 0), -1);
            Cv2.PutText(frame, $"{volumePct}%", new Point(45, 430), HersheyFonts.HersheySimplex, 0.6, Scalar.FromRgb(0, 255, 0), 2);
            Cv2.PutText(frame, "VOL", new Point(45, 135), HersheyFonts.HersheySimplex, 0.6, Scalar.FromRgb(255, 255, 255), 2);
        }
    }
}