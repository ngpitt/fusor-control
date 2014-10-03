using System;
using System.Collections;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;

namespace fusor_control_interface
{
    public partial class CalibrationForm : Form
    {
        public CalibrationForm(ControlForm form1)
        {
            InitializeComponent();

            this.form1 = form1;
            this.Shown += new EventHandler(calibrationFormShown);
            this.FormClosing += new FormClosingEventHandler(calibrationFormClose);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            update_delagate = new UpdateDelagate(updateForm);
            cleanup_delagate = new CleanupDelagate(cleanupForm);
            update_thread = new Thread(updateThread);
        }

        private bool run = false;
        private double pressure, count, voltage, current;
        private ControlForm form1;
        private Thread update_thread;
        private delegate void UpdateDelagate(bool pump, bool hv, double pressure, double count, double voltage, double current);
        private delegate void CleanupDelagate();
        private UpdateDelagate update_delagate;
        private CleanupDelagate cleanup_delagate;

        private void calibrationFormShown(object sender, EventArgs e)
        {
            label11.Text = "Samples: " + form1.samples[(int)Samples.PRESSURE_INPUT].Count;
            label15.Text = "Samples: " + form1.samples[(int)Samples.VOLTAGE_OUTPUT].Count;
            label24.Text = "Samples: " + form1.samples[(int)Samples.VOLTAGE_INPUT].Count;
            label28.Text = "Samples: " + form1.samples[(int)Samples.COUNT_INPUT].Count;
            label32.Text = "Samples: " + form1.samples[(int)Samples.CURRENT_INPUT].Count;

            numericUpDown1.Value = (decimal)form1.constants[(int)Constants.REGULATOR_SETPOINT];
            numericUpDown2.Value = (decimal)form1.constants[(int)Constants.REGULATOR_KP];
            numericUpDown3.Value = (decimal)form1.constants[(int)Constants.REGULATOR_KI];
            numericUpDown4.Value = (decimal)form1.constants[(int)Constants.REGULATOR_KD];

            numericUpDown8.Value = (decimal)form1.constants[(int)Constants.PRESSURE_MIN];
            numericUpDown9.Value = (decimal)form1.constants[(int)Constants.PRESSURE_MAX];
            numericUpDown10.Value = (decimal)form1.constants[(int)Constants.PRESSURE_KP];
            numericUpDown11.Value = (decimal)form1.constants[(int)Constants.PRESSURE_KI];
            numericUpDown12.Value = (decimal)form1.constants[(int)Constants.PRESSURE_KD];

            update_thread.Start();
        }

        private void calibrationFormClose(object sender, FormClosingEventArgs e)
        {
            if (run)
            {
                run = false;
                update_thread.Join();
                form1.serial_port.Dispose();
            }
            form1.setMenu(true);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            serialWrite((int)Commands.SET_PUMP_OUTPUT + " 1");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            serialWrite((int)Commands.SET_PUMP_OUTPUT + " 0");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            serialWrite((int)Commands.SET_HV_OUTPUT + " 1");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            serialWrite((int)Commands.SET_HV_OUTPUT + " 0");
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            form1.constants[(int)Constants.REGULATOR_SETPOINT] = (double)numericUpDown1.Value;
            serialWrite((int)Commands.SET_REGULATOR_SETPOINT + " " + form1.constants[(int)Constants.REGULATOR_SETPOINT]);
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            form1.constants[(int)Constants.REGULATOR_KP] = (double)numericUpDown2.Value;
            serialWrite((int)Commands.SET_REGULATOR_TUNINGS + " " + form1.constants[(int)Constants.REGULATOR_KP] + " " + form1.constants[(int)Constants.REGULATOR_KI] + " " + form1.constants[(int)Constants.REGULATOR_KD]);
        }

        private void numericUpDown3_ValueChanged(object sender, EventArgs e)
        {
            form1.constants[(int)Constants.REGULATOR_KI] = (double)numericUpDown3.Value;
            serialWrite((int)Commands.SET_REGULATOR_TUNINGS + " " + form1.constants[(int)Constants.REGULATOR_KP] + " " + form1.constants[(int)Constants.REGULATOR_KI] + " " + form1.constants[(int)Constants.REGULATOR_KD]);
        }

        private void numericUpDown4_ValueChanged(object sender, EventArgs e)
        {
            form1.constants[(int)Constants.REGULATOR_KD] = (double)numericUpDown4.Value;
            serialWrite((int)Commands.SET_REGULATOR_TUNINGS + " " + form1.constants[(int)Constants.REGULATOR_KP] + " " + form1.constants[(int)Constants.REGULATOR_KI] + " " + form1.constants[(int)Constants.REGULATOR_KD]);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            SortedList pressure_output_samples = new SortedList();

            if (form1.samples[(int)Samples.PRESSURE_INPUT].ContainsKey(pressure) || form1.samples[(int)Samples.PRESSURE_INPUT].ContainsValue(numericUpDown5.Value))
            {
                MessageBox.Show("Sample already exists.", "Fusor Control", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            form1.samples[(int)Samples.PRESSURE_INPUT].Add(pressure, numericUpDown5.Value);

            for (int i = 0; i < form1.samples[(int)Samples.PRESSURE_INPUT].Count; i++)
            {
                pressure_output_samples.Add(form1.samples[(int)Samples.PRESSURE_INPUT].GetByIndex(i), form1.samples[(int)Samples.PRESSURE_INPUT].GetKey(i));
            }

            form1.splines[(int)Splines.PRESSURE_INPUT] = calculateSpline(form1.samples[(int)Samples.PRESSURE_INPUT]);
            form1.splines[(int)Splines.PRESSURE_OUTPUT] = calculateSpline(pressure_output_samples);

            label11.Text = "Samples: " + form1.samples[(int)Samples.PRESSURE_INPUT].Count;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            form1.samples[(int)Samples.PRESSURE_INPUT].Clear();
            form1.splines[(int)Splines.PRESSURE_INPUT].Clear();
            form1.splines[(int)Splines.PRESSURE_OUTPUT].Clear();

            label11.Text = "Samples: " + form1.samples[(int)Samples.PRESSURE_INPUT].Count;
        }

        private void numericUpDown6_ValueChanged(object sender, EventArgs e)
        {
            serialWrite((int)Commands.SET_VOLTAGE_OUTPUT + " " + numericUpDown6.Value);
        }

        private void button7_Click(object sender, EventArgs e)
        {
            if (form1.samples[(int)Samples.VOLTAGE_OUTPUT].ContainsKey(numericUpDown7.Value) || form1.samples[(int)Samples.VOLTAGE_OUTPUT].ContainsValue(numericUpDown6.Value))
            {
                MessageBox.Show("Sample already exists.", "Fusor Control", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            form1.samples[(int)Samples.VOLTAGE_OUTPUT].Add(numericUpDown7.Value, numericUpDown6.Value);
            form1.splines[(int)Splines.VOLTAGE_OUTPUT] = calculateSpline(form1.samples[(int)Samples.VOLTAGE_OUTPUT]);

            label15.Text = "Samples: " + form1.samples[(int)Samples.VOLTAGE_OUTPUT].Count;
        }

        private void button8_Click(object sender, EventArgs e)
        {
            form1.samples[(int)Samples.VOLTAGE_OUTPUT].Clear();
            form1.splines[(int)Splines.VOLTAGE_OUTPUT].Clear();

            label15.Text = "Samples: " + form1.samples[(int)Samples.VOLTAGE_OUTPUT].Count;
        }

        private void numericUpDown8_ValueChanged(object sender, EventArgs e)
        {
            form1.constants[(int)Constants.PRESSURE_MIN] = (double)numericUpDown8.Value;
            serialWrite((int)Commands.SET_PRESSURE_LIMITS + " " + form1.constants[(int)Constants.PRESSURE_MIN] + " " + form1.constants[(int)Constants.PRESSURE_MAX]);
        }

        private void numericUpDown9_ValueChanged(object sender, EventArgs e)
        {
            form1.constants[(int)Constants.PRESSURE_MAX] = (double)numericUpDown9.Value;
            serialWrite((int)Commands.SET_PRESSURE_LIMITS + " " + form1.constants[(int)Constants.PRESSURE_MIN] + " " + form1.constants[(int)Constants.PRESSURE_MAX]);
        }

        private void numericUpDown10_ValueChanged(object sender, EventArgs e)
        {
            form1.constants[(int)Constants.PRESSURE_KP] = (double)numericUpDown10.Value;
            serialWrite((int)Commands.SET_PRESSURE_TUNINGS + " " + form1.constants[(int)Constants.PRESSURE_KP] + " " + form1.constants[(int)Constants.PRESSURE_KI] + " " + form1.constants[(int)Constants.PRESSURE_KD]);
        }

        private void numericUpDown11_ValueChanged(object sender, EventArgs e)
        {
            form1.constants[(int)Constants.PRESSURE_KI] = (double)numericUpDown11.Value;
            serialWrite((int)Commands.SET_PRESSURE_TUNINGS + " " + form1.constants[(int)Constants.PRESSURE_KP] + " " + form1.constants[(int)Constants.PRESSURE_KI] + " " + form1.constants[(int)Constants.PRESSURE_KD]);
        }

        private void numericUpDown12_ValueChanged(object sender, EventArgs e)
        {
            form1.constants[(int)Constants.PRESSURE_KD] = (double)numericUpDown12.Value;
            serialWrite((int)Commands.SET_PRESSURE_TUNINGS + " " + form1.constants[(int)Constants.PRESSURE_KP] + " " + form1.constants[(int)Constants.PRESSURE_KI] + " " + form1.constants[(int)Constants.PRESSURE_KD]);
        }

        private void button9_Click(object sender, EventArgs e)
        {
            if (form1.samples[(int)Samples.VOLTAGE_INPUT].ContainsKey(voltage) || form1.samples[(int)Samples.VOLTAGE_INPUT].ContainsValue(numericUpDown13.Value))
            {
                MessageBox.Show("Sample already exists.", "Fusor Control", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            form1.samples[(int)Samples.VOLTAGE_INPUT].Add(voltage, numericUpDown13.Value);
            form1.splines[(int)Splines.VOLTAGE_INPUT] = calculateSpline(form1.samples[(int)Samples.VOLTAGE_INPUT]);

            label24.Text = "Samples: " + form1.samples[(int)Samples.VOLTAGE_INPUT].Count;
        }

        private void button10_Click(object sender, EventArgs e)
        {
            form1.samples[(int)Samples.VOLTAGE_INPUT].Clear();
            form1.splines[(int)Splines.VOLTAGE_INPUT].Clear();

            label24.Text = "Samples: " + form1.samples[(int)Samples.VOLTAGE_INPUT].Count;
        }

        private void button11_Click(object sender, EventArgs e)
        {
            if (form1.samples[(int)Samples.COUNT_INPUT].ContainsKey(count) || form1.samples[(int)Samples.COUNT_INPUT].ContainsValue(numericUpDown14.Value))
            {
                MessageBox.Show("Sample already exists.", "Fusor Control", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            form1.samples[(int)Samples.COUNT_INPUT].Add(count, numericUpDown14.Value);
            form1.splines[(int)Splines.COUNT_INPUT] = calculateSpline(form1.samples[(int)Samples.COUNT_INPUT]);

            label28.Text = "Samples: " + form1.samples[(int)Samples.COUNT_INPUT].Count;
        }

        private void button12_Click(object sender, EventArgs e)
        {
            form1.samples[(int)Samples.COUNT_INPUT].Clear();
            form1.splines[(int)Splines.COUNT_INPUT].Clear();

            label28.Text = "Samples: " + form1.samples[(int)Samples.COUNT_INPUT].Count;
        }

        private void button13_Click(object sender, EventArgs e)
        {
            if (form1.samples[(int)Samples.CURRENT_INPUT].ContainsKey(current) || form1.samples[(int)Samples.CURRENT_INPUT].ContainsValue(numericUpDown15.Value))
            {
                MessageBox.Show("Sample already exists.", "Fusor Control", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            form1.samples[(int)Samples.CURRENT_INPUT].Add(current, numericUpDown15.Value);
            form1.splines[(int)Splines.CURRENT_INPUT] = calculateSpline(form1.samples[(int)Samples.CURRENT_INPUT]);

            label32.Text = "Samples: " + form1.samples[(int)Samples.CURRENT_INPUT].Count;
        }

        private void button14_Click(object sender, EventArgs e)
        {
            form1.samples[(int)Samples.CURRENT_INPUT].Clear();
            form1.splines[(int)Splines.CURRENT_INPUT].Clear();

            label32.Text = "Samples: " + form1.samples[(int)Samples.CURRENT_INPUT].Count;
        }

        private void updateThread()
        {
            bool pump, hv;
            int sleep;
            double pressure, voltage, current, count;
            ulong time = 0;
            Stopwatch stopwatch = new Stopwatch();

            run = true;

            while (run)
            {
                stopwatch.Restart();

                try
                {
                    form1.serial_port.WriteLine(Convert.ToString((int)Commands.GET_PUMP_INPUT));
                    pump = (form1.serial_port.ReadLine().Substring(0, 1) == "1");

                    form1.serial_port.WriteLine(Convert.ToString((int)Commands.GET_HV_INPUT));
                    hv = (form1.serial_port.ReadLine().Substring(0, 1) == "1");

                    form1.serial_port.WriteLine(Convert.ToString((int)Commands.GET_PRESSURE_INPUT));
                    pressure = Convert.ToDouble(form1.serial_port.ReadLine());

                    form1.serial_port.WriteLine(Convert.ToString((int)Commands.GET_VOLTAGE_INPUT));
                    voltage = Convert.ToDouble(form1.serial_port.ReadLine());

                    form1.serial_port.WriteLine(Convert.ToString((int)Commands.GET_CURRENT_INPUT));
                    current = Convert.ToDouble(form1.serial_port.ReadLine());

                    form1.serial_port.WriteLine(Convert.ToString((int)Commands.GET_COUNT_INPUT));
                    count = Convert.ToDouble(form1.serial_port.ReadLine());

                    this.BeginInvoke(update_delagate, pump, hv, pressure, count, voltage, current);
                }
                catch
                {
                    MessageBox.Show("Serial port disconnected.", "Fusor Control", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.BeginInvoke(cleanup_delagate);
                    return;
                }

                time += (ulong)form1.update_interval;
                sleep = (int)(form1.update_interval - stopwatch.ElapsedMilliseconds);

                if (sleep > 0)
                {
                    Thread.Sleep(sleep);
                }
            }
        }

        private void updateForm(bool pump, bool hv, double pressure, double count, double voltage, double current)
        {
            this.pressure = pressure;
            this.count = count;
            this.voltage = voltage;
            this.current = current;

            label3.Text = "Status: " + (pump ? "Normal" : "Accelerating/Stopped");
            label4.Text = "Status: " + (hv ? "On" : "Off");
            label9.Text = "Input: " + pressure;
            label22.Text = "Input: " + voltage;
            label26.Text = "Input: " + count;
            label30.Text = "Input: " + current;
        }

        private void cleanupForm()
        {
            this.Close();
        }

        private void serialWrite(string message)
        {
            try
            {
                form1.serial_port.WriteLine(message);
            }
            catch
            {
                MessageBox.Show("Serial port disconnected.", "Fusor Control", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
            }
        }

        private ArrayList calculateSpline(SortedList points)
        {
            int n = points.Count - 1;
            double[] x = new double[n + 1];
            double[] a = new double[n + 1];
            double[] b = new double[n];
            double[] d = new double[n];
            double[] h = new double[n];
            double[] alpha = new double[n];
            double[] c = new double[n + 1];
            double[] l = new double[n + 1];
            double[] mu = new double[n + 1];
            double[] z = new double[n + 1];
            ArrayList spline = new ArrayList();

            for (int i = 0; i <= n; i++)
            {
                x[i] = Convert.ToDouble(points.GetKey(i));
                a[i] = Convert.ToDouble(points.GetByIndex(i));
            }

            for (int i = 0; i <= n - 1; i++)
            {
                h[i] = x[i + 1] - x[i];
            }

            for (int i = 1; i <= n - 1; i++)
            {
                alpha[i] = 3 / h[i] * (a[i + 1] - a[i]) - 3 / h[i - 1] * (a[i] - a[i - 1]);
            }

            l[0] = 1;
            mu[0] = z[0] = 0;

            for (int i = 1; i <= n - 1; i++)
            {
                l[i] = 2 * (x[i + 1] - x[i - 1]) - h[i - 1] * mu[i - 1];
                mu[i] = h[i] / l[i];
                z[i] = (alpha[i] - h[i - 1] * z[i - 1]) / l[i];
            }

            l[n] = 1;
            z[n] = c[n] = 0;

            for (int j = n - 1; j >= 0; j--)
            {
                c[j] = z[j] - mu[j] * c[j + 1];
                b[j] = (a[j + 1] - a[j]) / h[j] - (h[j] * (c[j + 1] + 2 * c[j])) / 3;
                d[j] = (c[j + 1] - c[j]) / (3 * h[j]);
            }

            for (int i = 0; i <= n - 1; i++)
            {
                spline.Add(new double[] { a[i], b[i], c[i], d[i], x[i] });
            }

            return spline;
        }
    }
}