//------------------------------------------------------------------------------
//      Copyright (c) Microsoft Corporation.  All rights reserved.                                                             
//------------------------------------------------------------------------------

namespace Terrarium.Forms.Classes.Controls
{
	/// <summary>
	/// 
	/// </summary>
	partial class DeveloperPanel
	{
		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary> 
		/// Clean up any resources being used.
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

		#region Component Designer generated code

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DeveloperPanel));
            this.panel3 = new System.Windows.Forms.Panel();
            this.glassPanel2 = new Terrarium.Glass.GlassPanel();
            this.navigatePictureBox = new System.Windows.Forms.PictureBox();
            this.glassPanel3 = new Terrarium.Glass.GlassPanel();
            this.glassLabel3 = new Terrarium.Glass.GlassLabel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.gamePathLabel = new Terrarium.Glass.GlassLabel();
            this.plantCountLabel = new Terrarium.Glass.GlassLabel();
            this.glassLabel13 = new Terrarium.Glass.GlassLabel();
            this.maximumAnimalCountLabel = new Terrarium.Glass.GlassLabel();
            this.glassLabel15 = new Terrarium.Glass.GlassLabel();
            this.animalCountLabel = new Terrarium.Glass.GlassLabel();
            this.glassLabel9 = new Terrarium.Glass.GlassLabel();
            this.gameModeLabel = new Terrarium.Glass.GlassLabel();
            this.glassLabel4 = new Terrarium.Glass.GlassLabel();
            this.glassPanel1 = new Terrarium.Glass.GlassPanel();
            this.glassLabel1 = new Terrarium.Glass.GlassLabel();
            this.panel3.SuspendLayout();
            this.glassPanel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.navigatePictureBox)).BeginInit();
            this.glassPanel3.SuspendLayout();
            this.panel2.SuspendLayout();
            this.glassPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.glassPanel2);
            this.panel3.Controls.Add(this.glassPanel3);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel3.Location = new System.Drawing.Point(0, 292);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(200, 220);
            this.panel3.TabIndex = 18;
            // 
            // glassPanel2
            // 
            this.glassPanel2.Borders = ((Terrarium.Glass.GlassBorders)((((Terrarium.Glass.GlassBorders.Left | Terrarium.Glass.GlassBorders.Top)
                        | Terrarium.Glass.GlassBorders.Right)
                        | Terrarium.Glass.GlassBorders.Bottom)));
            this.glassPanel2.Controls.Add(this.navigatePictureBox);
            this.glassPanel2.Gradient.Bottom = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            this.glassPanel2.Gradient.Top = System.Drawing.Color.FromArgb(((int)(((byte)(96)))), ((int)(((byte)(96)))), ((int)(((byte)(96)))));
            this.glassPanel2.IsGlass = true;
            this.glassPanel2.IsSunk = false;
            this.glassPanel2.Location = new System.Drawing.Point(8, 30);
            this.glassPanel2.Name = "glassPanel2";
            this.glassPanel2.Size = new System.Drawing.Size(184, 184);
            this.glassPanel2.TabIndex = 11;
            this.glassPanel2.UseStyles = false;
            // 
            // navigatePictureBox
            // 
            this.navigatePictureBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.navigatePictureBox.BackColor = System.Drawing.Color.Gray;
            this.navigatePictureBox.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("navigatePictureBox.BackgroundImage")));
            this.navigatePictureBox.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.navigatePictureBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.navigatePictureBox.Location = new System.Drawing.Point(8, 8);
            this.navigatePictureBox.Name = "navigatePictureBox";
            this.navigatePictureBox.Size = new System.Drawing.Size(168, 168);
            this.navigatePictureBox.TabIndex = 11;
            this.navigatePictureBox.TabStop = false;
            this.navigatePictureBox.Paint += new System.Windows.Forms.PaintEventHandler(this.navigatePictureBox_Paint);
            // 
            // glassPanel3
            // 
            this.glassPanel3.Borders = ((Terrarium.Glass.GlassBorders)((((Terrarium.Glass.GlassBorders.Left | Terrarium.Glass.GlassBorders.Top)
                        | Terrarium.Glass.GlassBorders.Right)
                        | Terrarium.Glass.GlassBorders.Bottom)));
            this.glassPanel3.Controls.Add(this.glassLabel3);
            this.glassPanel3.Dock = System.Windows.Forms.DockStyle.Top;
            this.glassPanel3.Gradient.Bottom = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.glassPanel3.Gradient.Top = System.Drawing.Color.FromArgb(((int)(((byte)(96)))), ((int)(((byte)(96)))), ((int)(((byte)(96)))));
            this.glassPanel3.IsGlass = true;
            this.glassPanel3.IsSunk = false;
            this.glassPanel3.Location = new System.Drawing.Point(0, 0);
            this.glassPanel3.Name = "glassPanel3";
            this.glassPanel3.Size = new System.Drawing.Size(200, 24);
            this.glassPanel3.TabIndex = 3;
            this.glassPanel3.UseStyles = true;
            // 
            // glassLabel3
            // 
            this.glassLabel3.BackColor = System.Drawing.Color.Transparent;
            this.glassLabel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.glassLabel3.Font = new System.Drawing.Font("Verdana", 6.75F, System.Drawing.FontStyle.Bold);
            this.glassLabel3.ForeColor = System.Drawing.Color.White;
            this.glassLabel3.Location = new System.Drawing.Point(0, 0);
            this.glassLabel3.Name = "glassLabel3";
            this.glassLabel3.NoWrap = false;
            this.glassLabel3.Size = new System.Drawing.Size(200, 24);
            this.glassLabel3.TabIndex = 0;
            this.glassLabel3.Text = " Mini Map";
            this.glassLabel3.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.gamePathLabel);
            this.panel2.Controls.Add(this.plantCountLabel);
            this.panel2.Controls.Add(this.glassLabel13);
            this.panel2.Controls.Add(this.maximumAnimalCountLabel);
            this.panel2.Controls.Add(this.glassLabel15);
            this.panel2.Controls.Add(this.animalCountLabel);
            this.panel2.Controls.Add(this.glassLabel9);
            this.panel2.Controls.Add(this.gameModeLabel);
            this.panel2.Controls.Add(this.glassLabel4);
            this.panel2.Controls.Add(this.glassPanel1);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(0, 0);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(200, 292);
            this.panel2.TabIndex = 19;
            // 
            // gamePathLabel
            // 
            this.gamePathLabel.BackColor = System.Drawing.Color.Transparent;
            this.gamePathLabel.Font = new System.Drawing.Font("Verdana", 6.75F, System.Drawing.FontStyle.Bold);
            this.gamePathLabel.ForeColor = System.Drawing.Color.White;
            this.gamePathLabel.Location = new System.Drawing.Point(13, 47);
            this.gamePathLabel.Name = "gamePathLabel";
            this.gamePathLabel.NoWrap = false;
            this.gamePathLabel.Size = new System.Drawing.Size(174, 35);
            this.gamePathLabel.TabIndex = 40;
            this.gamePathLabel.Text = "XXXXX";
            // 
            // plantCountLabel
            // 
            this.plantCountLabel.BackColor = System.Drawing.Color.Transparent;
            this.plantCountLabel.Font = new System.Drawing.Font("Verdana", 6.75F, System.Drawing.FontStyle.Bold);
            this.plantCountLabel.ForeColor = System.Drawing.Color.White;
            this.plantCountLabel.Location = new System.Drawing.Point(115, 122);
            this.plantCountLabel.Name = "plantCountLabel";
            this.plantCountLabel.NoWrap = false;
            this.plantCountLabel.Size = new System.Drawing.Size(82, 12);
            this.plantCountLabel.TabIndex = 29;
            this.plantCountLabel.Text = "0";
            this.plantCountLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // glassLabel13
            // 
            this.glassLabel13.BackColor = System.Drawing.Color.Transparent;
            this.glassLabel13.Font = new System.Drawing.Font("Verdana", 6.75F, System.Drawing.FontStyle.Bold);
            this.glassLabel13.ForeColor = System.Drawing.Color.White;
            this.glassLabel13.Location = new System.Drawing.Point(3, 122);
            this.glassLabel13.Name = "glassLabel13";
            this.glassLabel13.NoWrap = false;
            this.glassLabel13.Size = new System.Drawing.Size(106, 14);
            this.glassLabel13.TabIndex = 28;
            this.glassLabel13.Text = "Current Plants:";
            this.glassLabel13.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // maximumAnimalCountLabel
            // 
            this.maximumAnimalCountLabel.BackColor = System.Drawing.Color.Transparent;
            this.maximumAnimalCountLabel.Font = new System.Drawing.Font("Verdana", 6.75F, System.Drawing.FontStyle.Bold);
            this.maximumAnimalCountLabel.ForeColor = System.Drawing.Color.White;
            this.maximumAnimalCountLabel.Location = new System.Drawing.Point(115, 106);
            this.maximumAnimalCountLabel.Name = "maximumAnimalCountLabel";
            this.maximumAnimalCountLabel.NoWrap = false;
            this.maximumAnimalCountLabel.Size = new System.Drawing.Size(82, 12);
            this.maximumAnimalCountLabel.TabIndex = 27;
            this.maximumAnimalCountLabel.Text = "0";
            this.maximumAnimalCountLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // glassLabel15
            // 
            this.glassLabel15.BackColor = System.Drawing.Color.Transparent;
            this.glassLabel15.Font = new System.Drawing.Font("Verdana", 6.75F, System.Drawing.FontStyle.Bold);
            this.glassLabel15.ForeColor = System.Drawing.Color.White;
            this.glassLabel15.Location = new System.Drawing.Point(3, 106);
            this.glassLabel15.Name = "glassLabel15";
            this.glassLabel15.NoWrap = false;
            this.glassLabel15.Size = new System.Drawing.Size(129, 12);
            this.glassLabel15.TabIndex = 26;
            this.glassLabel15.Text = "Maximum Animals:";
            this.glassLabel15.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // animalCountLabel
            // 
            this.animalCountLabel.BackColor = System.Drawing.Color.Transparent;
            this.animalCountLabel.Font = new System.Drawing.Font("Verdana", 6.75F, System.Drawing.FontStyle.Bold);
            this.animalCountLabel.ForeColor = System.Drawing.Color.White;
            this.animalCountLabel.Location = new System.Drawing.Point(115, 90);
            this.animalCountLabel.Name = "animalCountLabel";
            this.animalCountLabel.NoWrap = false;
            this.animalCountLabel.Size = new System.Drawing.Size(82, 12);
            this.animalCountLabel.TabIndex = 25;
            this.animalCountLabel.Text = "0";
            this.animalCountLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // glassLabel9
            // 
            this.glassLabel9.BackColor = System.Drawing.Color.Transparent;
            this.glassLabel9.Font = new System.Drawing.Font("Verdana", 6.75F, System.Drawing.FontStyle.Bold);
            this.glassLabel9.ForeColor = System.Drawing.Color.White;
            this.glassLabel9.Location = new System.Drawing.Point(3, 90);
            this.glassLabel9.Name = "glassLabel9";
            this.glassLabel9.NoWrap = false;
            this.glassLabel9.Size = new System.Drawing.Size(120, 12);
            this.glassLabel9.TabIndex = 24;
            this.glassLabel9.Text = "Current Animals:";
            this.glassLabel9.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // gameModeLabel
            // 
            this.gameModeLabel.BackColor = System.Drawing.Color.Transparent;
            this.gameModeLabel.Font = new System.Drawing.Font("Verdana", 6.75F, System.Drawing.FontStyle.Bold);
            this.gameModeLabel.ForeColor = System.Drawing.Color.White;
            this.gameModeLabel.Location = new System.Drawing.Point(115, 31);
            this.gameModeLabel.Name = "gameModeLabel";
            this.gameModeLabel.NoWrap = false;
            this.gameModeLabel.Size = new System.Drawing.Size(82, 12);
            this.gameModeLabel.TabIndex = 21;
            this.gameModeLabel.Text = "XXXXX";
            this.gameModeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // glassLabel4
            // 
            this.glassLabel4.BackColor = System.Drawing.Color.Transparent;
            this.glassLabel4.Font = new System.Drawing.Font("Verdana", 6.75F, System.Drawing.FontStyle.Bold);
            this.glassLabel4.ForeColor = System.Drawing.Color.White;
            this.glassLabel4.Location = new System.Drawing.Point(3, 31);
            this.glassLabel4.Name = "glassLabel4";
            this.glassLabel4.NoWrap = false;
            this.glassLabel4.Size = new System.Drawing.Size(82, 12);
            this.glassLabel4.TabIndex = 20;
            this.glassLabel4.Text = "Game Mode:";
            this.glassLabel4.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // glassPanel1
            // 
            this.glassPanel1.Borders = ((Terrarium.Glass.GlassBorders)(((Terrarium.Glass.GlassBorders.Left | Terrarium.Glass.GlassBorders.Right)
                        | Terrarium.Glass.GlassBorders.Bottom)));
            this.glassPanel1.Controls.Add(this.glassLabel1);
            this.glassPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.glassPanel1.Gradient.Bottom = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.glassPanel1.Gradient.Top = System.Drawing.Color.FromArgb(((int)(((byte)(96)))), ((int)(((byte)(96)))), ((int)(((byte)(96)))));
            this.glassPanel1.IsGlass = true;
            this.glassPanel1.IsSunk = false;
            this.glassPanel1.Location = new System.Drawing.Point(0, 0);
            this.glassPanel1.Name = "glassPanel1";
            this.glassPanel1.Size = new System.Drawing.Size(200, 24);
            this.glassPanel1.TabIndex = 1;
            this.glassPanel1.UseStyles = true;
            // 
            // glassLabel1
            // 
            this.glassLabel1.BackColor = System.Drawing.Color.Transparent;
            this.glassLabel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.glassLabel1.Font = new System.Drawing.Font("Verdana", 6.75F, System.Drawing.FontStyle.Bold);
            this.glassLabel1.ForeColor = System.Drawing.Color.White;
            this.glassLabel1.Location = new System.Drawing.Point(0, 0);
            this.glassLabel1.Name = "glassLabel1";
            this.glassLabel1.NoWrap = false;
            this.glassLabel1.Size = new System.Drawing.Size(200, 24);
            this.glassLabel1.TabIndex = 0;
            this.glassLabel1.Text = " Status";
            this.glassLabel1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // DeveloperPanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Navy;
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel3);
            this.Name = "DeveloperPanel";
            this.Size = new System.Drawing.Size(200, 512);
            this.panel3.ResumeLayout(false);
            this.glassPanel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.navigatePictureBox)).EndInit();
            this.glassPanel3.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.glassPanel1.ResumeLayout(false);
            this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Panel panel2;
        private Terrarium.Glass.GlassPanel glassPanel2;
        private System.Windows.Forms.PictureBox navigatePictureBox;
        private Terrarium.Glass.GlassPanel glassPanel3;
        private Terrarium.Glass.GlassLabel glassLabel3;
        private Terrarium.Glass.GlassLabel gamePathLabel;
        private Terrarium.Glass.GlassLabel plantCountLabel;
        private Terrarium.Glass.GlassLabel glassLabel13;
        private Terrarium.Glass.GlassLabel maximumAnimalCountLabel;
        private Terrarium.Glass.GlassLabel glassLabel15;
        private Terrarium.Glass.GlassLabel animalCountLabel;
        private Terrarium.Glass.GlassLabel glassLabel9;
        private Terrarium.Glass.GlassLabel gameModeLabel;
        private Terrarium.Glass.GlassLabel glassLabel4;
        private Terrarium.Glass.GlassPanel glassPanel1;
        private Terrarium.Glass.GlassLabel glassLabel1;
	}
}
