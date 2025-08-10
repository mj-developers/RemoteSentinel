using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace RemoteSentinel
{
    public sealed class CredentialsForm : Form
    {
        private readonly TextBox _txtHost;
        private readonly TextBox _txtUser;
        private readonly TextBox _txtPass;

        public string Host => _txtHost.Text.Trim();
        public string Username => _txtUser.Text.Trim();
        public string Password => _txtPass.Text;

        // Constructor sin valores (primer uso)
        public CredentialsForm() : this("", "", "") { }

        // Constructor con valores por defecto (para "Configurar credenciales…")
        public CredentialsForm(string defaultHost, string defaultUser, string defaultPass)
        {
            const int LOGO_SIZE = 128;
            const int LOGO_BOTTOM_MARGIN = 16;

            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10f);
            Text = "Configuración RemoteSentinel";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // ✅ Mostrar en la barra de tareas y usar icono personalizado
            ShowInTaskbar = true;
            try
            {
                var iconPath = Path.Combine(AppContext.BaseDirectory, "ico", "icon.ico");
                if (File.Exists(iconPath))
                    Icon = new Icon(iconPath);
            }
            catch { /* si falla, sin problema: icono por defecto */ }

            ShowInTaskbar = true;
            ClientSize = new Size(600, 460);
            Padding = new Padding(16);
            BackColor = SystemColors.Control;

            var card = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(card);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                AutoSize = false
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            card.Controls.Add(root);

            // Logo obligatorio
            var logoHost = new Panel { AutoSize = true, Anchor = AnchorStyles.None, Margin = new Padding(0, 0, 0, LOGO_BOTTOM_MARGIN) };
            var logoPath = Path.Combine(AppContext.BaseDirectory, "img", "Logo.jpg");
            if (!File.Exists(logoPath))
            {
                MessageBox.Show(this, $"No se encontró la imagen del logo en:\n{logoPath}",
                    "Logo requerido", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                using var raw = Image.FromFile(logoPath);
                var pb = new PictureBox
                {
                    Size = new Size(LOGO_SIZE, LOGO_SIZE),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = MakeCircular(raw, LOGO_SIZE),
                    Margin = new Padding(0)
                };
                logoHost.Controls.Add(pb);
                logoHost.Resize += (_, __) => pb.Location = new Point((logoHost.Width - pb.Width) / 2, 0);
            }
            root.Controls.Add(logoHost);

            var lblTitle = new Label
            {
                Text = "Introduce tus credenciales",
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Anchor = AnchorStyles.None
            };
            root.Controls.Add(lblTitle);
            root.Controls.Add(new Panel { Height = 10, AutoSize = true });

            var fields = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0, 0, 0, 0)
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380f));
            root.Controls.Add(fields);

            // Host
            var lblHost = new Label { Text = "Host:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 6) };
            _txtHost = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3), Text = defaultHost };
#if NET6_0_OR_GREATER
            if (string.IsNullOrWhiteSpace(_txtHost.Text))
                _txtHost.PlaceholderText = "Introduce la IP del servidor";
#endif
            fields.Controls.Add(lblHost, 0, 0);
            fields.Controls.Add(_txtHost, 1, 0);

            // Usuario
            var lblUser = new Label { Text = "Usuario:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 6) };
            _txtUser = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3), Text = defaultUser };
#if NET6_0_OR_GREATER
            if (string.IsNullOrWhiteSpace(_txtUser.Text))
                _txtUser.PlaceholderText = "Introduce el usuario";
#endif
            fields.Controls.Add(lblUser, 0, 1);
            fields.Controls.Add(_txtUser, 1, 1);

            // Contraseña
            var lblPass = new Label { Text = "Contraseña:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 6) };
            _txtPass = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3), UseSystemPasswordChar = true, Text = defaultPass };
#if NET6_0_OR_GREATER
            if (string.IsNullOrWhiteSpace(_txtPass.Text))
                _txtPass.PlaceholderText = "Introduce la contraseña";
#endif
            fields.Controls.Add(lblPass, 0, 2);
            fields.Controls.Add(_txtPass, 1, 2);

            // Botones centrados
            var buttonsRow = new TableLayoutPanel
            {
                AutoSize = true,
                Anchor = AnchorStyles.None,
                ColumnCount = 2,
                Margin = new Padding(0, 18, 0, 0)
            };
            buttonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            buttonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var btnCancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, AutoSize = true, Margin = new Padding(0) };
            var btnOk = new Button { Text = "Aceptar", DialogResult = DialogResult.OK, AutoSize = true, Margin = new Padding(12, 0, 0, 0) };
            buttonsRow.Controls.Add(btnCancel, 0, 0);
            buttonsRow.Controls.Add(btnOk, 1, 0);

            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                ColumnCount = 1
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            footer.Controls.Add(buttonsRow, 0, 0);
            card.Controls.Add(footer);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Shown += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(_txtHost.Text)) _txtHost.Focus();
                else if (string.IsNullOrWhiteSpace(_txtUser.Text)) _txtUser.Focus();
                else _txtPass.Focus();
            };

            // Validación simple
            btnOk.Click += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(_txtHost.Text))
                { MessageBox.Show(this, "Introduce el host.", "Falta host", MessageBoxButtons.OK, MessageBoxIcon.Warning); _txtHost.Focus(); DialogResult = DialogResult.None; return; }
                if (string.IsNullOrWhiteSpace(_txtUser.Text))
                { MessageBox.Show(this, "Introduce el usuario.", "Falta usuario", MessageBoxButtons.OK, MessageBoxIcon.Warning); _txtUser.Focus(); DialogResult = DialogResult.None; return; }
                if (string.IsNullOrWhiteSpace(_txtPass.Text))
                { MessageBox.Show(this, "Introduce la contraseña.", "Falta contraseña", MessageBoxButtons.OK, MessageBoxIcon.Warning); _txtPass.Focus(); DialogResult = DialogResult.None; return; }
            };
        }

        // Imagen circular con borde
        private static Image MakeCircular(Image src, int size)
        {
            var bmp = new Bitmap(size, size);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, size - 1, size - 1);
            g.SetClip(path);
            g.DrawImage(src, new Rectangle(0, 0, size, size));
            g.ResetClip();

            using var pen = new Pen(Color.FromArgb(210, 210, 210));
            g.DrawEllipse(pen, 0, 0, size - 1, size - 1);
            return bmp;
        }
    }
}
