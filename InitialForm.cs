using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace byWednesday
{
    public partial class InitialForm : Form
    {
        public InitialForm()
        {
            InitializeComponent();
            this.BackgroundImage = Image.FromFile("whu1.jpg");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (textBox1.Text == "368" && textBox2.Text == "")
            {
                DialogResult = DialogResult.OK;
                Dispose();
                Close();
            }
            else
            {
                MessageBox.Show("用户名或密码错误，请重新输入");
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            MessageBox.Show("本软件最终解释权归开发者所有，未经授权不得商用！！！");
        }
        #region useless
        private void 软件说明ToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }
        #endregion
    }
}
