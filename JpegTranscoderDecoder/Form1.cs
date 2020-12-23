using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JpegTranscoderDecoder
{
    public partial class Form1 : Form
    {
        private JpegDecompressor jpegDecompressor;

        private string saveFileName;

        public Form1()
        {
            InitializeComponent();
        }

        private void openButton_Click(object sender, EventArgs e)
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    //Get the path of specified file
                    var filePath = openFileDialog.FileName;
                    textBox1.Text = filePath;
                }
            }
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show("Разархивирование завершено");
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            var filePath = textBox1.Text;
            if (!File.Exists(filePath))
            {
                MessageBox.Show("Файл не существует", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var reader = new FileStream(filePath, FileMode.Open);
            var inputStream = new BitInputStream(reader);
            jpegDecompressor = new JpegDecompressor(inputStream);
            jpegDecompressor.Decompress(saveFileName);
            inputStream.Close();
        }

        private void decompressButton_Click(object sender, EventArgs e)
        {
            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.Filter = "JPG files (*.jpg)|*.jpg";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    saveFileName = saveFileDialog.FileName;
                    backgroundWorker1.RunWorkerAsync();
                }
            }
        }
    }
}