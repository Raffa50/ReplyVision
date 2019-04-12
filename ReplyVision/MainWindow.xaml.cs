using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ReplyVision
{
    public partial class MainWindow : Window
    {
        const string subscriptionKey = "e19d5b22624340f091739be8b8c96a48",
            faceEndpoint = "https://westeurope.api.cognitive.microsoft.com";

        private IList<DetectedFace> faceList;
        private string[] faceDescriptions;
        // The resize factor for the displayed image.
        private double resizeFactor;

        private const string defaultStatusBarText =
            "Place the mouse pointer over a face to see the face description.";

        private readonly IFaceClient faceClient;

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var openDlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JPEG Image(*.jpg)|*.jpg"
            };
            bool? result = openDlg.ShowDialog(this);

            // Return if canceled.
            if (!result.Value)
            {
                return;
            }

            var fileUri = new Uri(openDlg.FileName);
            var bitmapSource = new BitmapImage();

            bitmapSource.BeginInit();
            bitmapSource.CacheOption = BitmapCacheOption.None;
            bitmapSource.UriSource = fileUri;
            bitmapSource.EndInit();

            FacePhoto.Source = bitmapSource;

            await DetectFaces( openDlg.FileName, bitmapSource );
        }

        private async Task DetectFaces( string filePath, BitmapImage bitmapSource ) {
            Title = "Detecting...";
            faceList = await UploadAndDetectFaces( filePath );
            Title = String.Format( "Detection Finished. {0} face(s) detected", faceList.Count );

            if( faceList.Count > 0 ) {
                // Prepare to draw rectangles around the faces.
                var visual = new DrawingVisual();
                DrawingContext drawingContext = visual.RenderOpen();
                drawingContext.DrawImage(
                    bitmapSource,
                    new Rect( 0, 0, bitmapSource.Width, bitmapSource.Height ) );
                double dpi = bitmapSource.DpiX;
                // Some images don't contain dpi info.
                resizeFactor = ( dpi == 0 ) ? 1 : 96 / dpi;
                faceDescriptions = new String[ faceList.Count ];

                for( int i = 0; i < faceList.Count; ++i ) {
                    DetectedFace face = faceList[i];

                    // Draw a rectangle on the face.
                    drawingContext.DrawRectangle(
                        Brushes.Transparent,
                        new Pen( Brushes.Red, 2 ),
                        new Rect(
                            face.FaceRectangle.Left * resizeFactor,
                            face.FaceRectangle.Top * resizeFactor,
                            face.FaceRectangle.Width * resizeFactor,
                            face.FaceRectangle.Height * resizeFactor ) );

                    // Store the face description.
                    faceDescriptions[i] = FaceDescription( face );
                }

                drawingContext.Close();

                // Display the image with the rectangle around the face.
                var faceWithRectBitmap = new RenderTargetBitmap(
                    (int) ( bitmapSource.PixelWidth * resizeFactor ),
                    (int) ( bitmapSource.PixelHeight * resizeFactor ),
                    96,
                    96,
                    PixelFormats.Pbgra32 );

                faceWithRectBitmap.Render( visual );
                FacePhoto.Source = faceWithRectBitmap;

                // Set the status bar text.
                faceDescriptionStatusBar.Text = defaultStatusBarText;
            }
        }

        private string FaceDescription(DetectedFace face)
        {
            var sb = new StringBuilder("Face: ");

            // Add the gender, age, and smile.
            sb.Append(face.FaceAttributes.Gender).Append(", ")
                .Append(face.FaceAttributes.Age).Append(", ")
                .Append(String.Format("smile {0:F1}%, ", face.FaceAttributes.Smile * 100))
                .Append("Emotion: ");

            Emotion emotionScores = face.FaceAttributes.Emotion;

            var dict = new Dictionary<string, double>
            {
                { "Anger", emotionScores.Anger },
                { "Comptempt", emotionScores.Contempt },
                { "Disgust", emotionScores.Disgust },
                { "Fear", emotionScores.Fear },
                { "Happy", emotionScores.Happiness },
                { "Neutral", emotionScores.Neutral },
                { "Sad", emotionScores.Sadness },
                { "Surprise", emotionScores.Surprise }
            };

            var max = dict.First();
            foreach( var kv in dict ) {
                if( kv.Value > max.Value )
                    max = kv;
            }

            sb.Append( max.Key ).Append( ", " );

            if(face.FaceAttributes.Makeup != null && face.FaceAttributes.Makeup.LipMakeup)
                sb.Append( "Lip makeup, " );

            if( face.FaceAttributes.FacialHair != null ) {
                if( face.FaceAttributes.FacialHair.Beard > 0.25f )
                    sb.Append( "Beard, " );

                if( face.FaceAttributes.FacialHair.Moustache > 0.25f )
                    sb.Append( "Moustache, " );
            }

            // Add glasses.
            sb.Append(face.FaceAttributes.Glasses).Append(", ");

            // Add hair.
            sb.Append("Hair: ");

            // Display baldness confidence if over 1%.
            if( face.FaceAttributes.Hair.Bald > 0.80f )
                sb.Append( "Calvo " );
            else if( face.FaceAttributes.Hair.Bald >= 0.20f )
                sb.Append( "Stempiato " );
            else
                sb.Append( "Coi capelli " );

            // Display all hair color attributes over 10%.
            IList<HairColor> hairColors = face.FaceAttributes.Hair.HairColor;
            foreach (HairColor hairColor in hairColors)
            {
                if (hairColor.Confidence >= 0.1f)
                {
                    sb.Append(hairColor.Color.ToString());
                    sb.Append(String.Format(" {0:F1}% ", hairColor.Confidence * 100));
                }
            }

            // Return the built string.
            return sb.ToString();
        }

        // Displays the face description when the mouse is over a face rectangle.
        private void FacePhoto_MouseMove(object sender, MouseEventArgs e)
        {
            if (faceList == null)
                return;

            Point mouseXY = e.GetPosition(FacePhoto);

            ImageSource imageSource = FacePhoto.Source;
            var bitmapSource = (BitmapSource)imageSource;

            // Scale adjustment between the actual size and displayed size.
            var scale = FacePhoto.ActualWidth / (bitmapSource.PixelWidth / resizeFactor);

            // Check if this mouse position is over a face rectangle.
            bool mouseOverFace = false;

            for (int i = 0; i < faceList.Count; ++i)
            {
                FaceRectangle fr = faceList[i].FaceRectangle;
                double left = fr.Left * scale;
                double top = fr.Top * scale;
                double width = fr.Width * scale;
                double height = fr.Height * scale;

                // Display the face description if the mouse is over this face rectangle.
                if (mouseXY.X >= left && mouseXY.X <= left + width &&
                    mouseXY.Y >= top && mouseXY.Y <= top + height)
                {
                    faceDescriptionStatusBar.Text = faceDescriptions[i];
                    mouseOverFace = true;
                    break;
                }
            }

            // String to display when the mouse is not over a face rectangle.
            if (!mouseOverFace) faceDescriptionStatusBar.Text = defaultStatusBarText;
        }

        private async Task<IList<DetectedFace>> UploadAndDetectFaces(string imageFilePath)
        {
            // The list of Face attributes to return.
            var faceAttributes =new FaceAttributeType[]
                {
                    FaceAttributeType.Gender, FaceAttributeType.Age,
                    FaceAttributeType.Smile, FaceAttributeType.Emotion,
                    FaceAttributeType.Glasses, FaceAttributeType.Hair,
                    FaceAttributeType.Makeup, FaceAttributeType.FacialHair
                };

            // Call the Face API.
            try
            {
                using (Stream imageFileStream = File.OpenRead(imageFilePath))
                {
                    // The second argument specifies to return the faceId, while
                    // the third argument specifies not to return face landmarks.
                    IList<DetectedFace> faceList =
                        await faceClient.Face.DetectWithStreamAsync(
                            imageFileStream, true, false, faceAttributes);
                    return faceList;
                }
            }
            catch (APIErrorException f)
            {
                MessageBox.Show(f.Message);
                return new List<DetectedFace>();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error");
                return new List<DetectedFace>();
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            faceClient = new FaceClient(
                new ApiKeyServiceClientCredentials( subscriptionKey ),
                new DelegatingHandler[] {} ) {
                Endpoint = faceEndpoint
            };
        }
    }
}
