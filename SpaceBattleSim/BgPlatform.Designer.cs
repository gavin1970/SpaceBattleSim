using Chizl.ThreadSupport;

namespace SpaceBattleSim
{
    partial class BgPlatform
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
            Timer_Emp_Cleanup = new System.Windows.Forms.Timer(components);
            SuspendLayout();
            // 
            // Timer_Emp_Cleanup
            // 
            Timer_Emp_Cleanup.Enabled = true;
            Timer_Emp_Cleanup.Interval = 1000;
            Timer_Emp_Cleanup.Tick += Timer_Emp_Cleanup_Tick;
            // 
            // BgPlatform
            // 
            BackColor = Color.FromArgb(5, 5, 5);
            ClientSize = new Size(1113, 611);
            FormBorderStyle = FormBorderStyle.Fixed3D;
            Name = "BgPlatform";
            ResumeLayout(false);
        }
        #endregion

        private System.Windows.Forms.Timer Timer_Emp_Cleanup;
    }
}
