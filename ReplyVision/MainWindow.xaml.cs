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
        const string subscriptionKey = "c16bbd8cd72144faa33ab693e34b90d5",
            faceEndpoint = "https://westeurope.api.cognitive.microsoft.com",
            defaultStatusBarText = "Place the mouse pointer over a face to see the face description.";

        private string ImagePath;
        private BitmapImage image;
        private IList<DetectedFace> faceList;
        private string[] faceDescriptions;
        // The resize factor for the displayed image.
        private double resizeFactor;

        private readonly IFaceClient faceClient;
        private PersonDb personDb;
        private bool Selecting;
        private Int32Rect selectedFace;

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var openDlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JPEG Image(*.jpg)|*.jpg"
            };
            bool? result = openDlg.ShowDialog(this);

            if (!result.Value)
                return;

            ImagePath = openDlg.FileName;
            image = new BitmapImage(new Uri(ImagePath));

            FacePhoto.Source = image;

            await DetectFaces(ImagePath, image);
        }

        private async Task DetectFaces( string filePath, BitmapImage bitmapSource ) {
            Title = "Detecting...";
            faceList = await UploadAndDetectFaces( filePath );
            Title = string.Format( "Detection Finished. {0} face(s) detected", faceList.Count );

            if( faceList.Count > 0 ) {
                // Prepare to draw rectangles around the faces.
                var visual = new DrawingVisual();

                using (DrawingContext drawingContext = visual.RenderOpen())
                {
                    drawingContext.DrawImage(
                        bitmapSource,
                        new Rect(0, 0, bitmapSource.Width, bitmapSource.Height));
                    double dpi = bitmapSource.DpiX;
                    // Some images don't contain dpi info.
                    resizeFactor = (dpi == 0) ? 1 : 96 / dpi;
                    faceDescriptions = new string[faceList.Count];

                    for (int i = 0; i < faceList.Count; ++i)
                    {
                        DetectedFace face = faceList[i];

                        var rect = new Rect(
                                face.FaceRectangle.Left * resizeFactor,
                                face.FaceRectangle.Top * resizeFactor,
                                face.FaceRectangle.Width * resizeFactor,
                                face.FaceRectangle.Height * resizeFactor);

                        drawingContext.DrawRectangle(
                            Brushes.Transparent,
                            new Pen(Brushes.Red, 2),
                            rect);

                        faceDescriptions[i] = await FaceDescriptionAsync(face);
                    }
                }

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

        private async Task<string> FaceDescriptionAsync(DetectedFace face)
        {
            var sb = new StringBuilder("Face: ");

            // Add the gender, age, and smile.
            sb.Append(face.FaceAttributes.Gender).Append(", ")
                .Append(face.FaceAttributes.Age).Append(", ")
                .Append(string.Format("smile {0:F1}%, ", face.FaceAttributes.Smile * 100))
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
                    sb.Append(string.Format(" {0:F1}% ", hairColor.Confidence * 100));
                }
            }

            var results = await faceClient.Face.IdentifyAsync(new[] { face.FaceId.Value }, "team");
            foreach (var identifyResult in results)
            {
                Console.WriteLine("Result of face: {0}", identifyResult.FaceId);
                if (identifyResult.Candidates.Count == 0)
                {
                    Console.WriteLine("No one identified");
                }
                else
                {
                    // Get top 1 among all candidates returned
                    var candidateId = identifyResult.Candidates[0].PersonId;
                    var person = await faceClient.PersonGroupPerson.GetAsync("team", candidateId);
                    sb.Append(" Recognized: ").Append(person.Name);
                }
            }

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
                double left = fr.Left * scale,
                    top = fr.Top * scale,
                    width = fr.Width * scale,
                    height = fr.Height * scale;

                // Display the face description if the mouse is over this face rectangle.
                if (mouseXY.X >= left && mouseXY.X <= left + width &&
                    mouseXY.Y >= top && mouseXY.Y <= top + height)
                {
                    faceDescriptionStatusBar.Text = faceDescriptions[i];
                    selectedFace = new Int32Rect((int)(fr.Left / resizeFactor), (int)(fr.Top / resizeFactor), 
                        (int)(fr.Width / resizeFactor), (int)(fr.Height / resizeFactor));
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            personDb.Save("persons.json");
        }

        private async void Addbutton_Click(object sender, RoutedEventArgs e)
        {
            var openDlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JPEG Image(*.jpg)|*.jpg",
                Multiselect = true
            };
            bool? result = openDlg.ShowDialog(this);

            if (!result.Value)
                return;

            string nameSurname;
            if (!new InputBox("enter name", "name surname").ShowDialog(out nameSurname))
                return;

            if (!personDb.TryGetValue(nameSurname, out Guid personId))
            {
                var person = await faceClient.PersonGroupPerson.CreateAsync("team", nameSurname);
                personId = person.PersonId;
            }

            foreach (var imgpPath in openDlg.FileNames)
            {
                using (var stream = File.OpenRead(imgpPath))
                {
                    await faceClient.PersonGroupPerson.AddFaceFromStreamAsync("team", personId, stream);
                }
            }

            await faceClient.PersonGroup.TrainAsync("team");
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (await faceClient.PersonGroup.GetAsync("team") == null)
                await faceClient.PersonGroup.CreateAsync("team", "team");
        }

        private void ClickAddButton_Click(object sender, RoutedEventArgs e)
        {
            Selecting = true;
            Cursor = Cursors.Hand;
        }

        private Stream GetImageStream(BitmapSource source)
        {
            var mStream = new MemoryStream();
            var jEncoder = new JpegBitmapEncoder();
            jEncoder.Frames.Add(BitmapFrame.Create(source));  //the croppedBitmap is a CroppedBitmap object 
            jEncoder.QualityLevel = 75;
            jEncoder.Save(mStream);
            return mStream;
        }

        private async void FacePhoto_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!Selecting)
                return;
            Selecting = false;
            Cursor = Cursors.Arrow;

            string nameSurname;
            if (!new InputBox("enter name", "name surname").ShowDialog(out nameSurname))
                return;

            if (!personDb.TryGetValue(nameSurname, out Guid personId))
            {
                var person = await faceClient.PersonGroupPerson.CreateAsync("team", nameSurname);
                personId = person.PersonId;
            }

            var cb = new CroppedBitmap(image, selectedFace);
            using (var stream = GetImageStream(cb))
            {
                await faceClient.PersonGroupPerson.AddFaceFromStreamAsync("team", personId, stream);
            }

            await faceClient.PersonGroup.TrainAsync("team");
        }

        private async void RecoButton_Click(object sender, RoutedEventArgs e)
        {
            if(image != null)
                await DetectFaces(ImagePath, image);
        }

        public MainWindow()
        {
            InitializeComponent();

            faceClient = new FaceClient(
                new ApiKeyServiceClientCredentials( subscriptionKey ),
                new DelegatingHandler[] {} ) {
                Endpoint = faceEndpoint
            };

            personDb = PersonDb.Load("persons.json");
        }
    }
}
