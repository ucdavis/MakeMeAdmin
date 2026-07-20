//
// Copyright © 2010-2025, Sinclair Community College
// Licensed under the GNU General Public License, version 3.
// See the LICENSE file in the project root for full license information.
//

namespace SinclairCC.MakeMeAdmin
{
    using System;
    using System.Drawing;
    using System.Windows.Forms;

    /// <summary>
    /// An isolated Phase 3 test surface. It never contacts the Make Me Admin service.
    /// </summary>
    internal sealed class WindowsHelloPrototypeForm : Form
    {
        private readonly Button verifyButton;
        private readonly Label resultLabel;

        internal WindowsHelloPrototypeForm()
        {
            this.Text = "Make Me Admin - Windows Hello Prototype";
            this.Icon = Properties.Resources.SecurityLock;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(500, 185);

            Label explanation = new Label()
            {
                AutoSize = false,
                Location = new Point(20, 20),
                Size = new Size(460, 55),
                Text = "Phase 3 test only. This verifies the window-owned Windows Hello prompt and does not request administrator rights."
            };

            this.verifyButton = new Button()
            {
                Location = new Point(20, 88),
                Size = new Size(170, 32),
                Text = "Test Windows Hello",
                UseVisualStyleBackColor = true
            };
            this.verifyButton.Click += this.VerifyButtonClick;

            this.resultLabel = new Label()
            {
                AutoSize = false,
                Location = new Point(20, 135),
                Size = new Size(460, 30),
                Text = "Result: not run"
            };

            this.Controls.Add(explanation);
            this.Controls.Add(this.verifyButton);
            this.Controls.Add(this.resultLabel);
            this.AcceptButton = this.verifyButton;
        }

        private async void VerifyButtonClick(object sender, EventArgs e)
        {
            this.verifyButton.Enabled = false;
            this.resultLabel.Text = "Result: waiting for Windows Hello";

            try
            {
                WindowsHelloVerifier verifier = new WindowsHelloVerifier();
                WindowsHelloVerification verification = await verifier.VerifyAsync(
                    this.Handle,
                    "Confirm your identity to test Make Me Admin Windows Hello reauthentication.");

                this.resultLabel.Text = string.Format(
                    "Result: {0} (native value {1}; verified={2}; canceled={3})",
                    verification.Result,
                    verification.NativeValue,
                    verification.IsVerified,
                    verification.IsCanceled);
            }
            catch (Exception exception)
            {
                this.resultLabel.Text = string.Format(
                    "Error: {0} (0x{1:X8}) - {2}",
                    exception.GetType().Name,
                    exception.HResult,
                    exception.Message);
            }
            finally
            {
                this.verifyButton.Enabled = true;
            }
        }
    }
}
