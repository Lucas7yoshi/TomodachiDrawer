using SkiaSharp;
using SkiaSharp.Views.Desktop;

using System.ComponentModel;

using TomodachiDrawer.Core;
using TomodachiDrawer.Core.ImageProcessing;
using TomodachiDrawer.Core.OutputSinks;

namespace TomodachiDrawer.UI.Windows
{
    public partial class MainForm : Form
    {
        private string currentImagePath = string.Empty;

        public MainForm()
        {
            InitializeComponent();
            ColorMatcherComboBox.Items.Clear();
            ColorMatcherComboBox.Items.AddRange(ColourPalette.Quantizers.Keys.ToArray());
            ColorMatcherComboBox.SelectedIndex = 0;
            ColorMatcherComboBox.SelectedIndexChanged += (s, e) => UpdatePreview();


#if !DEBUG
            this.Size = new Size(934, 651);
#endif
            // Create a backround worker that repeatedly scans for the RP2040 being connected
            var backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += (s, e) =>
            {
                bool lastState = false;
                while (true)
                {
                    var rp2040Path = UF2Flasher.FindRP2040Drive();
                    if (rp2040Path != null)
                    {
                        // Update the UI with the found path
                        this.Invoke((MethodInvoker)delegate
                        {
                            OutputRP2040StatusLabel.Text = $"RP2040 found: {rp2040Path}";
                            OutputRP2040StatusLabel.ForeColor = Color.Green;

                            // enable shit
                            FlashBaseFirmwareButton.Enabled = true;
                            ExportRP2040Button.Enabled = !string.IsNullOrEmpty(currentImagePath);
                            if (!lastState)
                            {
                                CrappyLogBox.AppendText($"RP2040 connected @ {rp2040Path}\r\n");
                                lastState = true;
                            }
                        });

                    }
                    else
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            OutputRP2040StatusLabel.Text = "RP2040 not found";
                            OutputRP2040StatusLabel.ForeColor = Color.Red;

                            FlashBaseFirmwareButton.Enabled = false;
                            ExportRP2040Button.Enabled = false;
                            if (lastState)
                            {
                                CrappyLogBox.AppendText($"RP2040 disconnected...\r\n");
                                lastState = false;
                            }
                        });
                    }
                    Thread.Sleep(1000);
                }
            };
            backgroundWorker.RunWorkerAsync();
        }

        private void LoadImage(string path)
        {
            if (File.Exists(path))
            {
                var img = SKBitmap.Decode(path);
                if (img.Width > 256 || img.Height > 256)
                {
                    MessageBox.Show($"{Path.GetFileName(path)} is too big! Max of 256x256.", "Image too big", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                else
                {
                    currentImagePath = path;
                    ImagePathBox.Text = path;
                    UpdatePreview();
                    Log($"Loaded image: {Path.GetFileName(path)} ({img.Width}x{img.Height})");
                }
            }
            else
            {
                Log($"File does not exist..? {path}");
            }
        }

        private void UpdatePreview()
        {
            if (!File.Exists(currentImagePath)) {
                Log($"File does not exist, cannot update preview: {currentImagePath}");
                return;
            }
            var pal = new ColourPalette(new DummySink());
            var selectedQuantizer = ColorMatcherComboBox.SelectedItem?.ToString();
            if (selectedQuantizer == null)
                return;
            var preview = pal.PreviewColourMapping(
                SKBitmap.Decode(currentImagePath),
                selectedQuantizer
            );

            var previewBitmap = preview.ToBitmap();
            previewPictureBox.Image = previewBitmap;
            Log($"Updated preview for {Path.GetFileName(currentImagePath)} using {selectedQuantizer}");
        }

        private void OpenImageButton_Click(object sender, EventArgs e)
        {
#if DEBUG
            // If shift is pressed
            if (Control.ModifierKeys == Keys.Shift)
            {
                //string filePath = @"E:\Downloads\fox_real_256x256.png";
                //string filePath = @"E:\Downloads\32x32_fox.png";
                //currentImagePath = filePath;
                //ImagePathBox.Text = filePath;
                //UpdatePreview();
                LoadImage(@"E:\Downloads\32x32_fox.png");
                return;
            }
#endif
            if (openImageFileDialog.ShowDialog() == DialogResult.OK)
            {
                //string filePath = openImageFileDialog.FileName;
                //currentImagePath = filePath;
                //ImagePathBox.Text = filePath;

                //UpdatePreview();
                LoadImage(openImageFileDialog.FileName);
            }
        }

        private void Log(string msg)
        {
            Console.WriteLine(msg);
            this.BeginInvoke((MethodInvoker)delegate { CrappyLogBox.AppendText(msg + "\r\n"); });
        }

        private async void OutputSaveButton_Click(object sender, EventArgs e)
        {
            if (saveOutputFileDialog.ShowDialog() == DialogResult.OK)
            {
                var outputPath = saveOutputFileDialog.FileName;
                var imagePath = currentImagePath;
                var quantizer = ColorMatcherComboBox.SelectedItem!.ToString()!;
                var tspLimit = (float)TSPTimeLimitUpDown.Value;

                OutputSaveButton.Enabled = false;
                CrappyLogBox.AppendText("Starting export...\r\n");

                await Task.Run(async () =>
                {
                    var fileOutput = new FileControllerSink(outputPath);
                    var drawer = new CanvasDrawer(fileOutput, Log);
                    drawer.ConnectAndConfirmController();
                    await drawer.DrawImage(SKBitmap.Decode(imagePath), quantizer, tspLimit);
                    fileOutput.Dispose();
                });

                OutputSaveButton.Enabled = true;
                CrappyLogBox.AppendText("Export complete.\r\n");
            }
        }

        private Dictionary<PaletteColour, SKBitmap> colourLayersDebug = new();

        #region DEBUG GROUP BOX STUFF
        private void DebugColourLayersButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentImagePath) || !File.Exists(currentImagePath))
                return;
            var cp = new ColourPalette(new DummySink());
            var quantized = cp.QuantizeImage(
                SKBitmap.Decode(currentImagePath),
                ColorMatcherComboBox.SelectedItem.ToString()
            );
            var layers = cp.BuildFineLayers(quantized);
            colourLayersDebug.Clear();
            if (DebugFineTestCheckBox.Checked)
            {
                var cd = new CanvasDrawer(new DummySink(), Log);
                foreach (var l in layers)
                {
                    cd.DetectUniformAreas(l);
                }
            }
            if (!DebugShowBiggerCheckBox.Checked)
            {
                foreach (var layer in layers)
                {
                    var bitmap = new SKBitmap(quantized.GetLength(0), quantized.GetLength(1));
                    foreach (var p in layer.FineDetailPoints)
                    {
                        bitmap.SetPixel(
                            p.X,
                            p.Y,
                            new SKColor(layer.Colour.R, layer.Colour.G, layer.Colour.B)
                        );
                    }

                    colourLayersDebug[layer.Colour] = bitmap;
                }
            }
            else
            {
                foreach (var layer in layers)
                {
                    var bitmap = new SKBitmap(quantized.GetLength(0), quantized.GetLength(1));
                    foreach (var kv in layer.StampsBySize)
                    {
                        // draw the bigger stamps.
                        int stampSize = kv.Key;
                        foreach (var p in kv.Value)
                        {
                            for (int x = -stampSize / 2; x <= stampSize / 2; x++)
                            {
                                for (int y = -stampSize / 2 ; y <= stampSize /2; y++)
                                {
                                    int drawX = p.X + x;
                                    int drawY = p.Y + y;
                                    if (drawX >= 0 && drawX < bitmap.Width && drawY >= 0 && drawY < bitmap.Height)
                                    {
                                        bitmap.SetPixel(
                                            drawX,
                                            drawY,
                                            new SKColor(layer.Colour.R, layer.Colour.G, layer.Colour.B)
                                        );
                                    }
                                }
                            }
                        }
                    }
                    colourLayersDebug[layer.Colour] = bitmap;
                }
            }


            debugColourComboBox.Items.Clear();
            foreach (var cld in colourLayersDebug)
            {
                debugColourComboBox.Items.Add(cld.Key.DisplayName);
            }
        }

        private void DebugColourComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // set the image box to the selected layer
            var layer = debugColourComboBox.SelectedIndex;
            if (layer == -1 || colourLayersDebug.Count == 0)
                return;
            var colour = colourLayersDebug.Keys.ElementAt(layer);
            previewPictureBox.Image = colourLayersDebug[colour].ToBitmap();
        }

        #endregion

        private void TSPSolverTimeLimitHelpButton_Click(object sender, EventArgs e)
        {
            string message = @"TSP Solver Time Limit refers to how much time is alloted to the TSP solver.
TSP refers to the Travelling Sales Person problem, which is finding the optimal route among a set of points.
This is used to find the optimal path for the pen tool to take while drawing to minimize drawing time.

For larger images, the TSP solver can take longer to find an optimal route, its also possible it will never even find an optimal route if there is too many points.
For 64x64, 0.5s is generally fine, anything largest you should consider giving it more time.

This time is how long it is alloted PER colour, so if an image has 30 different colours used, 0.5s will take 15 seconds.
The TSP solve is not used always, a simpler ""snaking"" algorithm is used if its quicker, or if TSP didnt find anything in time, which it sometimes is, mostly for large continuous areas of colour.";
            MessageBox.Show(message, "TSP Solver Time Limit", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void FlashBaseFirmwareButton_Click(object sender, EventArgs e)
        {
            // Flash the default firmware.
            const string firmwareFileName = "TomodachiDrawer.Firmware.uf2";
            // Flashing is a very complex process.
            if (File.Exists(firmwareFileName))
            {
                // Begin the very complicated process
                File.Copy(firmwareFileName, UF2Flasher.FindRP2040Drive() + "TomodachiDrawer.Firmware.uf2", true);
                // thats it lol
                // wait until it dismounts and reboots itself
                var timeout = DateTime.Now.AddSeconds(5);
                while (UF2Flasher.FindRP2040Drive() != null)
                {
                    if (DateTime.Now > timeout)
                    {
                        MessageBox.Show("Wrote file but expected it to reset itself by now, maybe try doing it manually..?", "Error flashing base firmware", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    Thread.Sleep(500);
                }
                MessageBox.Show("Base firmware flashed! You can now use the standard output button to output your images to it!\nIf this is your first time, its likely flashing red. Simply hold BOOT and plug it back in, or hold BOOT and press reset if you have it.");
                CrappyLogBox.AppendText("Flashed base firmware to RP2040\r\n");
            }
            else
            {
                MessageBox.Show("For some reason could not locate TomodachiDrawer.Firmware.uf2", "Error flashing base firmware", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OutputExplanationButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Your RP2040-Zero (or similar) needs two things in its memory (it's flash):\r\n- The code that reads the instructions to draw your image and pipe it to the switch\r\n- The instructions to draw your image.\r\n\r\n\r\nTo connect your device for flashing, hold down the \"BOOT\" button and plug it in, or hold \"BOOT\" and press \"RESET\" while it is connected.\r\n\r\nYou only need to flash the code/\"firmware\" once.\r\n\r\nYou then flash the image data onto it for each image, without needing to reflash the firmware.\r\n\r\nWhen you first install the firmware, it'll reset itself, flash yellow 3 times, and then flash red.\r\nFlashing red is expected, as that means it cannot find the image data.\r\nReconnect it using the same \"BOOT\" button steps as described above, load your image, and hit \"Export to RP2040\".\r\n\r\nAgain, it will reboot, but now you can unplug it and plug it into your switch.\r\n\r\nYOU MUST HAVE \"Pro Controller Wired Commmunication\" ENABLED.\r\nGo to system settings -> Controllers & Accessories -> Pro Controller Wired Communication\r\n");
        }

        private async void ExportRP2040Button_Click(object sender, EventArgs e)
        {
            var imagePath = currentImagePath;
            var quantizer = ColorMatcherComboBox.SelectedItem!.ToString()!;
            var tspLimit = (float)TSPTimeLimitUpDown.Value;

            ExportRP2040Button.Enabled = false;

            await Task.Run(async () =>
            {
                string tempOutputName = Path.Combine(Path.GetTempPath(), $"rp2040output{new Random().Next(1000000, 9999999)}.tdld");
                Log($"Exporting to RP2040 flash ({Path.GetFileName(tempOutputName)})");
                //var fileOutput = new FileControllerSink(tempOutputName);
                var timingSink = new TimingSink();
                var drawer = new CanvasDrawer(timingSink, Log);
                drawer.ConnectAndConfirmController();
                Log("Starting to generate inputs...");
                await drawer.DrawImage(SKBitmap.Decode(imagePath), quantizer, tspLimit, DebugDisableLargeStamps.Checked);
                //fileOutput.Dispose();

                Log($"True complete overall time is: {timingSink.TotalTime.TotalSeconds}s");
                // actually output now
                var fileSink = new FileControllerSink(tempOutputName);
                timingSink.ReplayTo(fileSink);
                fileSink.Dispose();

                var tdldBytes = File.ReadAllBytes(tempOutputName);
                var uf2bytes = UF2Flasher.BuildTDLDUF2(tdldBytes);
                if (uf2bytes != null && uf2bytes.Length > 0)
                {
                    File.WriteAllBytes(UF2Flasher.FindRP2040Drive() + "tdld_image.uf2", uf2bytes);
                    Log("Wrote to RP2040 flash. Unplug the RP2040 and plug it into the switch without holding any button.");
                }

                if (File.Exists(tempOutputName))
                {
                    // delete temp file.
                    File.Delete(tempOutputName);
                }
            });

            ExportRP2040Button.Enabled = true;
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
                if (files != null)
                {
                    LoadImage(files.First());
                }
            }
        }

        private void InGameSetupExplanation_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Setup in game is fairly straightforward.\r\n- Navigate to the palette house\r\n- Ensure you are on the \"advanced\" drawing UI\r\n- Ensure your top colour is set to Black (it is by default)\r\n- Set your cursor to the TOP LEFT of where you want the drawing to be.\r\n- Ensure the full area of the canvas that will be drawn is on screen.\r\n\r\nIf the canvas is zoomed in, it will cause the cursor to desync as the canvas moves when the cursor gets on the edges. Zooming out fully avoids this.\r\n\r\nIf your image is 256x256, set it all the way in the top left. If your image is smaller, set your cursor to where you want the topleft most pixel of your drawing to be.", "In Game Setup", MessageBoxButtons.OK);
        }
    }
}
