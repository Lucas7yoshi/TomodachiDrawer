namespace TomodachiDrawer.UI.Windows
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            previewGroupBox = new GroupBox();
            DitheringComboBox = new ComboBox();
            previewPictureBox = new PixelBox();
            label2 = new Label();
            label1 = new Label();
            ColorMatcherComboBox = new ComboBox();
            openImageFileDialog = new OpenFileDialog();
            inputGroupBox = new GroupBox();
            OpenImageButton = new Button();
            ImagePathBox = new TextBox();
            outputGroupBox = new GroupBox();
            InGameSetupExplanation = new Button();
            OutputExplanationButton = new Button();
            ExportRP2040Button = new Button();
            label6 = new Label();
            label5 = new Label();
            FlashBaseFirmwareButton = new Button();
            OutputRP2040StatusLabel = new Label();
            imageList1 = new ImageList(components);
            saveOutputFileDialog = new SaveFileDialog();
            debugColourComboBox = new ComboBox();
            DebugColourLayerButton = new Button();
            debugGroupBox = new GroupBox();
            OutputSaveButton = new Button();
            TSPTimeLimitUpDown = new NumericUpDown();
            label3 = new Label();
            routingGroupBox = new GroupBox();
            TSPSolverTimeLimitHelpButton = new Button();
            label7 = new Label();
            CrappyLogBox = new TextBox();
            groupBox1 = new GroupBox();
            previewGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)previewPictureBox).BeginInit();
            inputGroupBox.SuspendLayout();
            outputGroupBox.SuspendLayout();
            debugGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)TSPTimeLimitUpDown).BeginInit();
            routingGroupBox.SuspendLayout();
            groupBox1.SuspendLayout();
            SuspendLayout();
            // 
            // previewGroupBox
            // 
            previewGroupBox.Controls.Add(DitheringComboBox);
            previewGroupBox.Controls.Add(previewPictureBox);
            previewGroupBox.Controls.Add(label2);
            previewGroupBox.Controls.Add(label1);
            previewGroupBox.Controls.Add(ColorMatcherComboBox);
            previewGroupBox.Location = new Point(382, 12);
            previewGroupBox.Name = "previewGroupBox";
            previewGroupBox.Size = new Size(531, 593);
            previewGroupBox.TabIndex = 1;
            previewGroupBox.TabStop = false;
            previewGroupBox.Text = "Preview";
            // 
            // DitheringComboBox
            // 
            DitheringComboBox.FormattingEnabled = true;
            DitheringComboBox.Location = new Point(271, 37);
            DitheringComboBox.Name = "DitheringComboBox";
            DitheringComboBox.Size = new Size(247, 23);
            DitheringComboBox.TabIndex = 5;
            // 
            // previewPictureBox
            // 
            previewPictureBox.BorderStyle = BorderStyle.FixedSingle;
            previewPictureBox.Location = new Point(6, 66);
            previewPictureBox.Name = "previewPictureBox";
            previewPictureBox.Size = new Size(512, 512);
            previewPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            previewPictureBox.TabIndex = 4;
            previewPictureBox.TabStop = false;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(271, 19);
            label2.Name = "label2";
            label2.Size = new Size(59, 15);
            label2.TabIndex = 3;
            label2.Text = "Dithering:";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(6, 19);
            label1.Name = "label1";
            label1.Size = new Size(86, 15);
            label1.TabIndex = 2;
            label1.Text = "Color Matcher:";
            // 
            // ColorMatcherComboBox
            // 
            ColorMatcherComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            ColorMatcherComboBox.FormattingEnabled = true;
            ColorMatcherComboBox.Location = new Point(6, 37);
            ColorMatcherComboBox.Name = "ColorMatcherComboBox";
            ColorMatcherComboBox.Size = new Size(256, 23);
            ColorMatcherComboBox.TabIndex = 1;
            // 
            // inputGroupBox
            // 
            inputGroupBox.Controls.Add(OpenImageButton);
            inputGroupBox.Controls.Add(ImagePathBox);
            inputGroupBox.Location = new Point(12, 12);
            inputGroupBox.Name = "inputGroupBox";
            inputGroupBox.Size = new Size(358, 60);
            inputGroupBox.TabIndex = 2;
            inputGroupBox.TabStop = false;
            inputGroupBox.Text = "Input Image";
            // 
            // OpenImageButton
            // 
            OpenImageButton.Location = new Point(283, 21);
            OpenImageButton.Name = "OpenImageButton";
            OpenImageButton.Size = new Size(75, 23);
            OpenImageButton.TabIndex = 1;
            OpenImageButton.Text = "Open...";
            OpenImageButton.UseVisualStyleBackColor = true;
            OpenImageButton.Click += OpenImageButton_Click;
            // 
            // ImagePathBox
            // 
            ImagePathBox.Location = new Point(6, 22);
            ImagePathBox.Name = "ImagePathBox";
            ImagePathBox.ReadOnly = true;
            ImagePathBox.Size = new Size(271, 23);
            ImagePathBox.TabIndex = 0;
            // 
            // outputGroupBox
            // 
            outputGroupBox.Controls.Add(InGameSetupExplanation);
            outputGroupBox.Controls.Add(OutputExplanationButton);
            outputGroupBox.Controls.Add(ExportRP2040Button);
            outputGroupBox.Controls.Add(label6);
            outputGroupBox.Controls.Add(label5);
            outputGroupBox.Controls.Add(FlashBaseFirmwareButton);
            outputGroupBox.Controls.Add(OutputRP2040StatusLabel);
            outputGroupBox.Location = new Point(12, 463);
            outputGroupBox.Name = "outputGroupBox";
            outputGroupBox.Size = new Size(364, 142);
            outputGroupBox.TabIndex = 3;
            outputGroupBox.TabStop = false;
            outputGroupBox.Text = "RP2040 Output";
            // 
            // InGameSetupExplanation
            // 
            InGameSetupExplanation.Location = new Point(6, 66);
            InGameSetupExplanation.Name = "InGameSetupExplanation";
            InGameSetupExplanation.Size = new Size(352, 23);
            InGameSetupExplanation.TabIndex = 12;
            InGameSetupExplanation.Text = "Setup in game explanation";
            InGameSetupExplanation.UseVisualStyleBackColor = true;
            InGameSetupExplanation.Click += InGameSetupExplanation_Click;
            // 
            // OutputExplanationButton
            // 
            OutputExplanationButton.Location = new Point(6, 37);
            OutputExplanationButton.Name = "OutputExplanationButton";
            OutputExplanationButton.Size = new Size(352, 23);
            OutputExplanationButton.TabIndex = 11;
            OutputExplanationButton.Text = "Explanation";
            OutputExplanationButton.UseVisualStyleBackColor = true;
            OutputExplanationButton.Click += OutputExplanationButton_Click;
            // 
            // ExportRP2040Button
            // 
            ExportRP2040Button.Enabled = false;
            ExportRP2040Button.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            ExportRP2040Button.Location = new Point(202, 113);
            ExportRP2040Button.Name = "ExportRP2040Button";
            ExportRP2040Button.Size = new Size(156, 23);
            ExportRP2040Button.TabIndex = 10;
            ExportRP2040Button.Text = "Export To RP2040!";
            ExportRP2040Button.UseVisualStyleBackColor = true;
            ExportRP2040Button.Click += ExportRP2040Button_Click;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(239, 95);
            label6.Name = "label6";
            label6.Size = new Size(71, 15);
            label6.TabIndex = 9;
            label6.Text = "Write Image";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(11, 95);
            label5.Name = "label5";
            label5.Size = new Size(117, 15);
            label5.TabIndex = 8;
            label5.Text = "Flash Firmware Once";
            // 
            // FlashBaseFirmwareButton
            // 
            FlashBaseFirmwareButton.Enabled = false;
            FlashBaseFirmwareButton.Location = new Point(6, 113);
            FlashBaseFirmwareButton.Name = "FlashBaseFirmwareButton";
            FlashBaseFirmwareButton.Size = new Size(143, 23);
            FlashBaseFirmwareButton.TabIndex = 6;
            FlashBaseFirmwareButton.Text = "Flash Base Firmware";
            FlashBaseFirmwareButton.UseVisualStyleBackColor = true;
            FlashBaseFirmwareButton.Click += FlashBaseFirmwareButton_Click;
            // 
            // OutputRP2040StatusLabel
            // 
            OutputRP2040StatusLabel.AutoSize = true;
            OutputRP2040StatusLabel.Location = new Point(6, 19);
            OutputRP2040StatusLabel.Name = "OutputRP2040StatusLabel";
            OutputRP2040StatusLabel.Size = new Size(143, 15);
            OutputRP2040StatusLabel.TabIndex = 5;
            OutputRP2040StatusLabel.Text = "RP2040 Status: Not Found";
            // 
            // imageList1
            // 
            imageList1.ColorDepth = ColorDepth.Depth32Bit;
            imageList1.ImageSize = new Size(16, 16);
            imageList1.TransparentColor = Color.Transparent;
            // 
            // saveOutputFileDialog
            // 
            saveOutputFileDialog.DefaultExt = "tdld";
            saveOutputFileDialog.Filter = "Tomodachi Life Drawer file|*.tdld|All Files|*.*";
            // 
            // debugColourComboBox
            // 
            debugColourComboBox.FormattingEnabled = true;
            debugColourComboBox.Location = new Point(6, 22);
            debugColourComboBox.Name = "debugColourComboBox";
            debugColourComboBox.Size = new Size(214, 23);
            debugColourComboBox.TabIndex = 4;
            debugColourComboBox.SelectedIndexChanged += DebugColourComboBox_SelectedIndexChanged;
            // 
            // DebugColourLayerButton
            // 
            DebugColourLayerButton.Location = new Point(6, 51);
            DebugColourLayerButton.Name = "DebugColourLayerButton";
            DebugColourLayerButton.Size = new Size(214, 23);
            DebugColourLayerButton.TabIndex = 5;
            DebugColourLayerButton.Text = "Debug Colour Layers";
            DebugColourLayerButton.UseVisualStyleBackColor = true;
            DebugColourLayerButton.Click += DebugColourLayersButton_Click;
            // 
            // debugGroupBox
            // 
            debugGroupBox.Controls.Add(debugColourComboBox);
            debugGroupBox.Controls.Add(DebugColourLayerButton);
            debugGroupBox.Controls.Add(OutputSaveButton);
            debugGroupBox.Location = new Point(919, 12);
            debugGroupBox.Name = "debugGroupBox";
            debugGroupBox.Size = new Size(226, 593);
            debugGroupBox.TabIndex = 6;
            debugGroupBox.TabStop = false;
            debugGroupBox.Text = "Debug";
            // 
            // OutputSaveButton
            // 
            OutputSaveButton.Location = new Point(6, 80);
            OutputSaveButton.Name = "OutputSaveButton";
            OutputSaveButton.Size = new Size(214, 23);
            OutputSaveButton.TabIndex = 4;
            OutputSaveButton.Text = "Save .TDLD";
            OutputSaveButton.UseVisualStyleBackColor = true;
            OutputSaveButton.Click += OutputSaveButton_Click;
            // 
            // TSPTimeLimitUpDown
            // 
            TSPTimeLimitUpDown.DecimalPlaces = 1;
            TSPTimeLimitUpDown.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            TSPTimeLimitUpDown.Location = new Point(134, 17);
            TSPTimeLimitUpDown.Maximum = new decimal(new int[] { 30, 0, 0, 0 });
            TSPTimeLimitUpDown.Minimum = new decimal(new int[] { 1, 0, 0, 65536 });
            TSPTimeLimitUpDown.Name = "TSPTimeLimitUpDown";
            TSPTimeLimitUpDown.Size = new Size(120, 23);
            TSPTimeLimitUpDown.TabIndex = 7;
            TSPTimeLimitUpDown.Value = new decimal(new int[] { 5, 0, 0, 65536 });
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(6, 19);
            label3.Name = "label3";
            label3.Size = new Size(122, 15);
            label3.TabIndex = 8;
            label3.Text = "TSP Solver Time Limit";
            // 
            // routingGroupBox
            // 
            routingGroupBox.Controls.Add(TSPSolverTimeLimitHelpButton);
            routingGroupBox.Controls.Add(label7);
            routingGroupBox.Controls.Add(TSPTimeLimitUpDown);
            routingGroupBox.Controls.Add(label3);
            routingGroupBox.Location = new Point(12, 78);
            routingGroupBox.Name = "routingGroupBox";
            routingGroupBox.Size = new Size(358, 89);
            routingGroupBox.TabIndex = 9;
            routingGroupBox.TabStop = false;
            routingGroupBox.Text = "Routing";
            // 
            // TSPSolverTimeLimitHelpButton
            // 
            TSPSolverTimeLimitHelpButton.Location = new Point(316, 17);
            TSPSolverTimeLimitHelpButton.Name = "TSPSolverTimeLimitHelpButton";
            TSPSolverTimeLimitHelpButton.Size = new Size(23, 23);
            TSPSolverTimeLimitHelpButton.TabIndex = 11;
            TSPSolverTimeLimitHelpButton.Text = "?";
            TSPSolverTimeLimitHelpButton.UseVisualStyleBackColor = true;
            TSPSolverTimeLimitHelpButton.Click += TSPSolverTimeLimitHelpButton_Click;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(260, 19);
            label7.Name = "label7";
            label7.Size = new Size(50, 15);
            label7.TabIndex = 10;
            label7.Text = "seconds";
            // 
            // CrappyLogBox
            // 
            CrappyLogBox.Dock = DockStyle.Fill;
            CrappyLogBox.Location = new Point(3, 19);
            CrappyLogBox.Multiline = true;
            CrappyLogBox.Name = "CrappyLogBox";
            CrappyLogBox.ReadOnly = true;
            CrappyLogBox.ScrollBars = ScrollBars.Vertical;
            CrappyLogBox.Size = new Size(352, 262);
            CrappyLogBox.TabIndex = 10;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(CrappyLogBox);
            groupBox1.Location = new Point(12, 173);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(358, 284);
            groupBox1.TabIndex = 11;
            groupBox1.TabStop = false;
            groupBox1.Text = "Log";
            // 
            // MainForm
            // 
            AllowDrop = true;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1156, 612);
            Controls.Add(groupBox1);
            Controls.Add(routingGroupBox);
            Controls.Add(debugGroupBox);
            Controls.Add(outputGroupBox);
            Controls.Add(inputGroupBox);
            Controls.Add(previewGroupBox);
            Name = "MainForm";
            Text = "Tomodachi Drawer (UI.Windows)";
            DragDrop += MainForm_DragDrop;
            DragEnter += MainForm_DragEnter;
            previewGroupBox.ResumeLayout(false);
            previewGroupBox.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)previewPictureBox).EndInit();
            inputGroupBox.ResumeLayout(false);
            inputGroupBox.PerformLayout();
            outputGroupBox.ResumeLayout(false);
            outputGroupBox.PerformLayout();
            debugGroupBox.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)TSPTimeLimitUpDown).EndInit();
            routingGroupBox.ResumeLayout(false);
            routingGroupBox.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion
        private GroupBox previewGroupBox;
        private ComboBox ColorMatcherComboBox;
        private Label label1;
        private Label label2;
        private OpenFileDialog openImageFileDialog;
        private GroupBox inputGroupBox;
        private Button OpenImageButton;
        private TextBox ImagePathBox;
        private GroupBox outputGroupBox;
        private ImageList imageList1;
        private PixelBox previewPictureBox;
        private SaveFileDialog saveOutputFileDialog;
        private ComboBox debugColourComboBox;
        private Button DebugColourLayerButton;
        private GroupBox debugGroupBox;
        private ComboBox DitheringComboBox;
        private NumericUpDown TSPTimeLimitUpDown;
        private Label label3;
        private GroupBox routingGroupBox;
        private Button TSPSolverTimeLimitHelpButton;
        private Label label7;
        private Button FlashBaseFirmwareButton;
        private Label OutputRP2040StatusLabel;
        private Button OutputSaveButton;
        private TextBox CrappyLogBox;
        private Button ExportRP2040Button;
        private Label label6;
        private Label label5;
        private Button OutputExplanationButton;
        private Button InGameSetupExplanation;
        private GroupBox groupBox1;
    }
}
