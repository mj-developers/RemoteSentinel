using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace RemoteSentinel.UI
{
    /// <summary>
    /// Ventana para introducir o configurar las credenciales de conexión de RemoteSentinel.
    /// </summary>
    public sealed class CredentialsForm : Form
    {
        private readonly TextBox _txtHost;
        private readonly TextBox _txtUser;
        private readonly TextBox _txtPass;
        private readonly TextBox _txtAlias; // Alias local (obligatorio)

        public string Host => _txtHost.Text.Trim();
        public string Username => _txtUser.Text.Trim();
        public string Password => _txtPass.Text.Trim();
        public string Alias => _txtAlias.Text.Trim();

        /// Constructor sin parámetros (uso inicial o creación por defecto)
        public CredentialsForm() : this("", "", "", "") { }

        /// Constructor con valores por defecto (ej: al reconfigurar credenciales, sin alias)
        public CredentialsForm(string defaultHost, string defaultUser, string defaultPass)
            : this(defaultHost, defaultUser, defaultPass, "") { }

        /// Constructor con valores por defecto (incluye alias)
        public CredentialsForm(string defaultHost, string defaultUser, string defaultPass, string defaultAlias)
        {
            const int LOGO_SIZE = 128;
            const int LOGO_BOTTOM_MARGIN = 16;

            // Configuración general de la ventana
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10f);
            Text = "Configuración RemoteSentinel";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Mostrar en la barra de tareas y usar icono personalizado
            ShowInTaskbar = true;
            try
            {
                var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "ico", "icon.ico");
                if (File.Exists(iconPath))
                    Icon = new Icon(iconPath);
            }
            catch { }

            // Dimensiones y estilo
            ClientSize = new Size(600, 480);
            Padding = new Padding(16);
            BackColor = SystemColors.Control;

            // Panel principal tipo "tarjeta"
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(card);

            // Contenedor principal en formato de tabla
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

            // Logo
            var logoHost = new Panel { AutoSize = true, Anchor = AnchorStyles.None, Margin = new Padding(0, 0, 0, LOGO_BOTTOM_MARGIN) };
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "img", "Logo.jpg");
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

            // Título
            var lblTitle = new Label
            {
                Text = "Introduce tus credenciales",
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Anchor = AnchorStyles.None
            };
            root.Controls.Add(lblTitle);

            // Espaciador
            root.Controls.Add(new Panel { Height = 10, AutoSize = true });

            // Campos de entrada
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
            if (string.IsNullOrWhiteSpace(_txtHost.Text))
                _txtHost.PlaceholderText = "Introduce la IP del servidor";
            fields.Controls.Add(lblHost, 0, 0);
            fields.Controls.Add(_txtHost, 1, 0);

            // Usuario
            var lblUser = new Label { Text = "Usuario:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 6) };
            _txtUser = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3), Text = defaultUser };
            if (string.IsNullOrWhiteSpace(_txtUser.Text))
                _txtUser.PlaceholderText = "Introduce el usuario";
            fields.Controls.Add(lblUser, 0, 1);
            fields.Controls.Add(_txtUser, 1, 1);

            // Contraseña
            var lblPass = new Label { Text = "Contraseña:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 6) };
            _txtPass = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3), UseSystemPasswordChar = true, Text = defaultPass };
            if (string.IsNullOrWhiteSpace(_txtPass.Text))
                _txtPass.PlaceholderText = "Introduce la contraseña";
            fields.Controls.Add(lblPass, 0, 2);
            fields.Controls.Add(_txtPass, 1, 2);

            // Alias (obligatorio)
            var lblAlias = new Label { Text = "Alias:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 6) };
            _txtAlias = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3), Text = string.IsNullOrWhiteSpace(defaultAlias) ? "" : defaultAlias };
            if (string.IsNullOrWhiteSpace(_txtAlias.Text))
                _txtAlias.PlaceholderText = "Cómo te verán los demás";
            fields.Controls.Add(lblAlias, 0, 3);
            fields.Controls.Add(_txtAlias, 1, 3);

            // Botones
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

            // Pie de formulario
            var footer = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                ColumnCount = 1
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            footer.Controls.Add(buttonsRow, 0, 0);
            card.Controls.Add(footer);

            // Asignar acciones rápidas con Enter/Esc
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            // Foco inicial en el primer campo vacío
            Shown += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(_txtHost.Text)) _txtHost.Focus();
                else if (string.IsNullOrWhiteSpace(_txtUser.Text)) _txtUser.Focus();
                else if (string.IsNullOrWhiteSpace(_txtPass.Text)) _txtPass.Focus();
                else _txtAlias.Focus();
            };

            // Validación mínima al pulsar "Aceptar"
            btnOk.Click += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(_txtHost.Text))
                { MessageBox.Show(this, "Introduce el host.", "Falta host", MessageBoxButtons.OK, MessageBoxIcon.Warning); _txtHost.Focus(); DialogResult = DialogResult.None; return; }
                if (string.IsNullOrWhiteSpace(_txtUser.Text))
                { MessageBox.Show(this, "Introduce el usuario.", "Falta usuario", MessageBoxButtons.OK, MessageBoxIcon.Warning); _txtUser.Focus(); DialogResult = DialogResult.None; return; }
                if (string.IsNullOrWhiteSpace(_txtPass.Text))
                { MessageBox.Show(this, "Introduce la contraseña.", "Falta contraseña", MessageBoxButtons.OK, MessageBoxIcon.Warning); _txtPass.Focus(); DialogResult = DialogResult.None; return; }
                if (string.IsNullOrWhiteSpace(_txtAlias.Text))
                { MessageBox.Show(this, "Introduce un alias.", "Falta alias", MessageBoxButtons.OK, MessageBoxIcon.Warning); _txtAlias.Focus(); DialogResult = DialogResult.None; return; }
            };
        }

        /// <summary>
        /// Convierte una imagen cuadrada en un círculo con borde gris claro.
        /// </summary>
        private static Image MakeCircular(Image src, int size)
        {
            var bmp = new Bitmap(size, size);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Recorte circular
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, size - 1, size - 1);
            g.SetClip(path);
            g.DrawImage(src, new Rectangle(0, 0, size, size));
            g.ResetClip();

            // Borde circular
            using var pen = new Pen(Color.FromArgb(210, 210, 210));
            g.DrawEllipse(pen, 0, 0, size - 1, size - 1);
            return bmp;
        }
    }
}
