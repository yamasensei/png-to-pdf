using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace PngToPdf
{
    public class MainForm : Form
    {
        private ListBox listBox;
        private Button btnAdd, btnRemove, btnClear, btnUp, btnDown, btnExport;
        private ComboBox cmbPageSize;
        private Label lblCount, lblStatus, lblPageSize;
        private Panel headerPanel, footerPanel;

        public MainForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "PNG → PDF Converter";
            this.Size = new Size(520, 580);
            this.MinimumSize = new Size(520, 580);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(245, 245, 250);
            this.Font = new Font("Segoe UI", 9f);

            // ── Header ──────────────────────────────────────────────
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Color.FromArgb(99, 88, 234)
            };
            Label lblTitle = new Label
            {
                Text = "🖼  PNG → PDF Converter",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(16, 12)
            };
            headerPanel.Controls.Add(lblTitle);

            // ── Body ────────────────────────────────────────────────
            GroupBox grpList = new GroupBox
            {
                Text = "Danh sách ảnh",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(100, 100, 120),
                Location = new Point(14, 62),
                Size = new Size(478, 360),
                BackColor = Color.Transparent
            };

            listBox = new ListBox
            {
                Location = new Point(10, 22),
                Size = new Size(456, 270),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9f),
                SelectionMode = SelectionMode.One
            };

            // Button row
            btnAdd = MakeBtn("＋ Thêm ảnh", Color.FromArgb(99, 88, 234), Color.White);
            btnAdd.Location = new Point(10, 300);
            btnAdd.Click += BtnAdd_Click;

            btnUp = MakeBtn("↑ Lên", Color.FromArgb(220, 220, 235), Color.FromArgb(60, 60, 80));
            btnUp.Location = new Point(130, 300);
            btnUp.Click += BtnUp_Click;

            btnDown = MakeBtn("↓ Xuống", Color.FromArgb(220, 220, 235), Color.FromArgb(60, 60, 80));
            btnDown.Location = new Point(210, 300);
            btnDown.Click += BtnDown_Click;

            btnRemove = MakeBtn("✕ Xoá", Color.FromArgb(239, 83, 80), Color.White);
            btnRemove.Location = new Point(350, 300);
            btnRemove.Click += BtnRemove_Click;

            btnClear = MakeBtn("Xoá hết", Color.FromArgb(220, 220, 235), Color.FromArgb(60, 60, 80));
            btnClear.Location = new Point(420, 300);
            btnClear.Width = 48;
            btnClear.Click += BtnClear_Click;

            grpList.Controls.AddRange(new Control[] { listBox, btnAdd, btnUp, btnDown, btnRemove, btnClear });

            lblCount = new Label
            {
                Text = "0 ảnh",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(140, 140, 160),
                AutoSize = true,
                Location = new Point(448, 430)
            };

            // ── Export section ──────────────────────────────────────
            GroupBox grpExport = new GroupBox
            {
                Text = "Xuất PDF",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(100, 100, 120),
                Location = new Point(14, 438),
                Size = new Size(478, 76),
                BackColor = Color.Transparent
            };

            lblPageSize = new Label
            {
                Text = "Kích thước trang:",
                AutoSize = true,
                Location = new Point(10, 26),
                ForeColor = Color.FromArgb(50, 50, 70)
            };

            cmbPageSize = new ComboBox
            {
                Location = new Point(130, 22),
                Width = 140,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.White
            };
            cmbPageSize.Items.AddRange(new string[] { "Vừa khít ảnh", "A4 dọc", "A4 ngang" });
            cmbPageSize.SelectedIndex = 0;

            btnExport = new Button
            {
                Text = "📄  Xuất PDF",
                Location = new Point(310, 18),
                Size = new Size(156, 42),
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.FromArgb(5, 46, 22),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnExport.FlatAppearance.BorderSize = 0;
            btnExport.Click += BtnExport_Click;

            grpExport.Controls.AddRange(new Control[] { lblPageSize, cmbPageSize, btnExport });

            // ── Status bar ──────────────────────────────────────────
            footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                BackColor = Color.FromArgb(235, 235, 245)
            };
            lblStatus = new Label
            {
                Text = "Sẵn sàng",
                AutoSize = true,
                Location = new Point(10, 6),
                ForeColor = Color.FromArgb(120, 120, 140),
                Font = new Font("Segoe UI", 8f)
            };
            footerPanel.Controls.Add(lblStatus);

            this.Controls.AddRange(new Control[] {
                headerPanel, grpList, lblCount, grpExport, footerPanel
            });
        }

        private Button MakeBtn(string text, Color bg, Color fg)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(112, 30),
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        // ── Event handlers ──────────────────────────────────────────
        private List<string> imagePaths = new List<string>();

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Chọn ảnh PNG/JPG";
                dlg.Filter = "Ảnh|*.png;*.jpg;*.jpeg;*.bmp|Tất cả|*.*";
                dlg.Multiselect = true;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    int added = 0;
                    foreach (var f in dlg.FileNames)
                    {
                        if (!imagePaths.Contains(f)) { imagePaths.Add(f); added++; }
                    }
                    RefreshList();
                    SetStatus($"Đã thêm {added} ảnh");
                }
            }
        }

        private void BtnRemove_Click(object sender, EventArgs e)
        {
            if (listBox.SelectedIndex < 0) return;
            imagePaths.RemoveAt(listBox.SelectedIndex);
            RefreshList();
            SetStatus("Đã xoá ảnh đã chọn");
        }

        private void BtnClear_Click(object sender, EventArgs e)
        {
            if (imagePaths.Count == 0) return;
            if (MessageBox.Show("Xoá hết danh sách?", "Xác nhận",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                imagePaths.Clear();
                RefreshList();
                SetStatus("Đã xoá hết");
            }
        }

        private void BtnUp_Click(object sender, EventArgs e)
        {
            int i = listBox.SelectedIndex;
            if (i <= 0) return;
            var tmp = imagePaths[i]; imagePaths[i] = imagePaths[i - 1]; imagePaths[i - 1] = tmp;
            RefreshList();
            listBox.SelectedIndex = i - 1;
        }

        private void BtnDown_Click(object sender, EventArgs e)
        {
            int i = listBox.SelectedIndex;
            if (i < 0 || i >= imagePaths.Count - 1) return;
            var tmp = imagePaths[i]; imagePaths[i] = imagePaths[i + 1]; imagePaths[i + 1] = tmp;
            RefreshList();
            listBox.SelectedIndex = i + 1;
        }

        private void RefreshList()
        {
            listBox.Items.Clear();
            for (int i = 0; i < imagePaths.Count; i++)
                listBox.Items.Add($"  {i + 1:D2}.  {Path.GetFileName(imagePaths[i])}");
            lblCount.Text = $"{imagePaths.Count} ảnh";
        }

        private void SetStatus(string msg) => lblStatus.Text = msg;

        private void BtnExport_Click(object sender, EventArgs e)
        {
            if (imagePaths.Count == 0)
            {
                MessageBox.Show("Hãy thêm ít nhất 1 ảnh trước.", "Chưa có ảnh",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = "Lưu file PDF";
                dlg.Filter = "PDF|*.pdf";
                dlg.DefaultExt = "pdf";
                if (dlg.ShowDialog() != DialogResult.OK) return;

                try
                {
                    SetStatus("Đang xử lý...");
                    Cursor = Cursors.WaitCursor;
                    Application.DoEvents();

                    string mode = cmbPageSize.SelectedItem.ToString();
                    PdfWriter.WritePdf(dlg.FileName, imagePaths, mode);

                    SetStatus($"✅  Đã xuất: {Path.GetFileName(dlg.FileName)}");
                    MessageBox.Show($"Tạo PDF thành công!\n\n{dlg.FileName}",
                        "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    SetStatus("❌  Lỗi khi xuất PDF");
                    MessageBox.Show("Lỗi: " + ex.Message, "Lỗi",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally { Cursor = Cursors.Default; }
            }
        }
    }
}
