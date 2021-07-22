using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace byWednesday
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            InitialForm f1 = new InitialForm();
            Form1 form1 = new Form1();
            f1.ShowDialog();
            if (f1.DialogResult == DialogResult.OK)
            {
                Application.Run(form1);
            }
        }
    }
}
