using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using MathNet.Numerics.LinearAlgebra;
using NeuralNet;

using Windows.UI.Input;
using Windows.UI.Xaml.Shapes;
using Windows.UI;
using Windows.UI.Xaml.Media.Imaging;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Microsoft.FSharp.Core;
using System.Net.Http;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace MNISTApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page {

        Network network;
        PointerPoint currentPoint, oldPoint;
        DispatcherTimer timer;

        public MainPage()
        {
            this.InitializeComponent();

			network = new Network(NeuralNet.Activations.Sigmoid.Activation, NeuralNet.Activations.Sigmoid.Prime, NeuralSettings.weights, NeuralSettings.biases, null);
            timer = new DispatcherTimer();
			
            timer.Interval = TimeSpan.FromSeconds(1);

			HttpClient AzureClient = new HttpClient();
			AzureClient.BaseAddress = new System.Uri(APISettings.AzureBaseUrl);
			AzureClient.DefaultRequestHeaders.Add("Accept", "application/json");
			AzureClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + APISettings.AzureKey);

			HttpClient NeuralNetClient = new HttpClient();
			NeuralNetClient.BaseAddress = new System.Uri(APISettings.NeuralBaseUrl);
						
            EventHandler<object> ehl = null;
            ehl = async (s, args) => {
                timer.Stop();
                System.Diagnostics.Debug.WriteLine("stopped drawing");

				this.progressRing.IsActive = true;

				byte[] bytes = await CanvasToBytes(DrawingCanvas);

				int azureRecognized = 0;
				var azureRequest = PixelsToJSON(bytes);
				var azureResponse = await AzureClient.PostAsync(APISettings.AzureRequestUrl, new StringContent(azureRequest, System.Text.Encoding.UTF8, "application/json"));
				var azureResult = await azureResponse.Content.ReadAsStringAsync();
				azureRecognized = Int32.Parse(azureResult.Substring(azureResult.Length - "7\"]]}}}}".Length, 1)); //dont need to parse no json

				int nnRecognized = 0;
				var nnRequest = PixelsToCSV(bytes);
				var nnResponse = await NeuralNetClient.PostAsync(APISettings.NeuralRequestUrl, new StringContent("5"));
				System.Diagnostics.Debug.WriteLine(await nnResponse.Content.ReadAsStringAsync());
				
				this.progressRing.IsActive = false;
				
				Frame.Navigate(typeof(ResultPage), nnRecognized * 10 + azureRecognized);
                DrawingCanvas.Children.Clear();
            };
            timer.Tick += new EventHandler<object>(ehl);

            this.NavigationCacheMode = NavigationCacheMode.Required;
        }
		
        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // TODO: Prepare page for display here.

            // TODO: If your application contains multiple pages, ensure that you are
            // handling the hardware Back button by registering for the
            // Windows.Phone.UI.Input.HardwareButtons.BackPressed event.
            // If you are using the NavigationHelper provided by some templates,
            // this event is handled for you.
        }

        private async void AppBarButton_Click(object sender, RoutedEventArgs e) {
			var about = new MessageDialog("Neural.NET demo\n\nAndrew D'Angelo <dangeloandrew@outlook.com>\nhttp://andrew.uni.cx\nhttps://github.com/excelangue");
			await about.ShowAsync();
        }

        private void DrawingCanvas_PointerPressed(object sender, PointerRoutedEventArgs e) {
            timer.Stop();
            currentPoint = e.GetCurrentPoint(DrawingCanvas); // captures the first point
            oldPoint = currentPoint;
        }

        private void DrawingCanvas_PointerMoved(object sender, PointerRoutedEventArgs e) {
            currentPoint = e.GetCurrentPoint(this.DrawingCanvas);

            Line line = new Line() { X1 = currentPoint.Position.X, Y1 = currentPoint.Position.Y, X2 = oldPoint.Position.X, Y2 = oldPoint.Position.Y };
            line.Stroke = new SolidColorBrush(Colors.White);
            line.StrokeThickness = 50;

            line.StrokeStartLineCap = PenLineCap.Triangle;
            line.StrokeEndLineCap = PenLineCap.Triangle;
            line.StrokeLineJoin = PenLineJoin.Bevel;

            this.DrawingCanvas.Children.Add(line);
            oldPoint = currentPoint;

        }

        private void DrawingCanvas_PointerReleased(object sender, PointerRoutedEventArgs e) {
            timer.Start();
        }
        
        private async Task<byte[]> CanvasToBytes(Canvas canvas) {
			RenderTargetBitmap rtb = new RenderTargetBitmap();
			await rtb.RenderAsync(canvas, 14, 14);
			var pix = await rtb.GetPixelsAsync();

			IBuffer pixelBuffer = await rtb.GetPixelsAsync();
			byte[] pixels = pixelBuffer.ToArray();
			System.Diagnostics.Debug.WriteLine(pixels);

			return pixels;
		}

		private string PixelsToJSON(byte[] pixels) {
			double[] vals = new double[784];
			for (int i = 0; i < vals.Length; i++) {
				vals[i] = (double)(pixels[i * 4]);
			}

			var request = "{\"Inputs\":{\"input1\":{\"ColumnNames\":[\"Label\",";
			for (int i = 0; i < 783; i++) {
				request += "\"f" + i + "\",";
			}
			request += "\"f783\"],\"Values\":[[\"0\",";
			for (int i = 0; i < 783; i++) {
				request += "\"" + vals[i] + "\",";
			}
			request += "\"" + vals[783] + "\"]]}},\"GlobalParameters\": {}}}";

			return request;
		}

		private string PixelsToCSV(byte[] pixels) {
			var request = "";
			for (int i = 0; i < 784; i++) {
				request += ((double)(pixels[i * 4])).ToString() + ",";
			}

			return request;
		}
    }
}
