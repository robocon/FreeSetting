using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ThaiNationalIDCard;

namespace FreeSetting
{
    public partial class Form1 : Form
    {
        static readonly HttpClient client = new HttpClient();
        private ThaiIDCard idcard;
        public Personal person;

        private static System.Timers.Timer aTimer;
        public string[] cardReaders;
        private string testGetKeyChar = "";

        public string mophToken = "";

        public Form1()
        {
            InitializeComponent();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            loadingTxt.Hide();
            loadingIcon.Hide();
            errMsg.Text = "";

            //Bitmap Photo1 = new Bitmap(FreeSetting.Properties.Resources.avatar, new Size(160, 200));
            string mophToken = "";
            try
            {
                mophToken = await Task.Run(() => getToken());
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mophToken);
            }
            catch(Exception ex)
            {
                errMsg.Text = "การเชื่อมต่อมีปัญหา กรุณาตรวจสอบสัญญาณอินเตอร์เน็ต "+ex.Message;
            }

            this.KeyPreview = true;
            aTimer = new System.Timers.Timer(5000);
            aTimer.Elapsed += TimerElapsed;

            try
            {
                idcard = new ThaiIDCard();
                cardReaders = idcard.GetReaders();
                idcard.MonitorStart(cardReaders[0].ToString());
                idcard.eventCardInserted += new handleCardInserted(CardInsertedCallback);
                idcard.eventCardRemoved += new handleCardRemoved(CardRemoveCallback);
            }
            catch (Exception ex)
            {
                //label1SetText("ไม่พบเครื่องอ่านบัตร Smart Card");
                Console.WriteLine(ex.Message);
            }

        }

        public async Task<string> getToken()
        {
            byte[] key = Encoding.UTF8.GetBytes("$jwt@moph#");
            byte[] message = Encoding.UTF8.GetBytes("Surasak11512");

            HMACSHA256 hmac = new HMACSHA256(key);
            byte[] hash = hmac.ComputeHash(message);
            string hashHex = BitConverter.ToString(hash).Replace("-", "");

            var response = await client.GetAsync("https://cvp1.moph.go.th/token?Action=get_moph_access_token&user=Surasak11512&password_hash=" + hashHex + "&hospital_code=11512");
            response.EnsureSuccessStatusCode();
            var token = await response.Content.ReadAsStringAsync();
            return token;
        }

        private void CardRemoveCallback()
        {
            //label1SetText("");

            loadingTxt.BeginInvoke(new MethodInvoker(delegate { loadingTxt.Hide(); }));
            loadingIcon.BeginInvoke(new MethodInvoker(delegate { loadingIcon.Hide(); }));

            Bitmap Photo1 = new Bitmap(FreeSetting.Properties.Resources.avatar, new Size(160, 200));
            ptImage.BeginInvoke(new MethodInvoker(delegate { ptImage.Image = Photo1; }));


            ptName.BeginInvoke(new MethodInvoker(delegate { ptName.Text = "-"; }));
            ptIdcard.BeginInvoke(new MethodInvoker(delegate { ptIdcard.Text = "-"; }));
            ptAddress.BeginInvoke(new MethodInvoker(delegate { ptAddress.Text = "-"; }));
            ptRoad.BeginInvoke(new MethodInvoker(delegate { ptRoad.Text = "-"; }));
            ptMoo.BeginInvoke(new MethodInvoker(delegate { ptMoo.Text = "-"; }));
            ptTambol.BeginInvoke(new MethodInvoker(delegate { ptTambol.Text = "-"; }));
            ptAmp.BeginInvoke(new MethodInvoker(delegate { ptAmp.Text = "-"; }));
            ptProvince.BeginInvoke(new MethodInvoker(delegate { ptProvince.Text = "-"; }));


            vaccInfo.BeginInvoke(new MethodInvoker(delegate { vaccInfo.Text = "-"; }));

            errMsg.BeginInvoke(new MethodInvoker(delegate { errMsg.Text = ""; }));

            idcard.MonitorStop(cardReaders[0].ToString());
            Console.WriteLine("Remove Card : " + cardReaders[0].ToString() + " ");
        }

        private void TimerElapsed(object sender, EventArgs e)
        {
            Console.WriteLine("TimerElapsed: TIME STOPPPPPPP");
            aTimer.Enabled = false;
            testGetKeyChar = "";
        }

        // Run Card Reader
        public async Task<Personal> RunCardReadder()
        {
            Console.WriteLine("get data from cardreader");
            Personal person = await Task.Run(() => GetPersonalCardreader());
            return person;
        }

        // ดึงข้อมูลบัตรประชาชน
        public Personal GetPersonalCardreader()
        {
            Personal person = null;
            try
            {
                idcard = new ThaiIDCard();
                //Personal person = idcard.readAllPhoto();
                person = idcard.readAllPhoto();
            }
            catch (Exception ex)
            {
                //label1SetText("ขณะระบบกำลังอ่านบัตรประชาชน ไม่ควรดึงบัตรประชาชนออก\nกรุณาเสียบบัตรประชาชนอีกครั้ง");
                Console.WriteLine(ex.ToString());
            }
            return person;
        }

        private async void CardInsertedCallback(Personal person)
        {
            loadingTxt.BeginInvoke(new MethodInvoker(delegate { loadingTxt.Show(); }));
            loadingIcon.BeginInvoke(new MethodInvoker(delegate { loadingIcon.Show(); }));
            person = await RunCardReadder();
            if (person != null)
            {
                string preIdcard = person.Citizenid.Substring(0,3);
                string postIdcard = person.Citizenid.Substring(8);
                string viewIdcard = preIdcard + "XXXXX" + postIdcard;

                string historyString = "";
                try
                {
                    var response = await client.GetAsync("https://cvp1.moph.go.th/api/ImmunizationHistory?cid=" + person.Citizenid);
                    response.EnsureSuccessStatusCode();
                    historyString = await response.Content.ReadAsStringAsync();
                }
                catch(Exception ex)
                {
                    errMsg.BeginInvoke(new MethodInvoker(delegate { errMsg.Text = "การเชื่อมต่อมีปัญหา "+ex.Message; }));
                }

                if (string.IsNullOrEmpty(historyString))
                {
                    loadingTxt.BeginInvoke(new MethodInvoker(delegate { loadingTxt.Hide(); }));
                    loadingIcon.BeginInvoke(new MethodInvoker(delegate { loadingIcon.Hide(); }));
                    return;
                }
                

                JObject historyObj = JObject.Parse(historyString);

                JToken patient = historyObj["result"]["patient"];
                string ptFullNmae = patient["prefix"].ToString() + " " + patient["first_name"].ToString() + "  " + patient["last_name"].ToString();

                JToken ptAddr = patient["Address"].FirstOrDefault();
                ptName.BeginInvoke(new MethodInvoker(delegate { ptName.Text = ptFullNmae; }));
                ptIdcard.BeginInvoke(new MethodInvoker(delegate { ptIdcard.Text = viewIdcard; }));

                /*ptAddress.BeginInvoke(new MethodInvoker(delegate { ptAddress.Text = ptAddr["Address"].ToString(); }));
                ptRoad.BeginInvoke(new MethodInvoker(delegate { ptRoad.Text = ptAddr["road"].ToString(); }));
                ptMoo.BeginInvoke(new MethodInvoker(delegate { ptMoo.Text = ptAddr["moo"].ToString(); }));
                ptTambol.BeginInvoke(new MethodInvoker(delegate { ptTambol.Text = ptAddr["tmb_name"].ToString(); }));
                ptAmp.BeginInvoke(new MethodInvoker(delegate { ptAmp.Text = ptAddr["amp_name"].ToString(); }));
                ptProvince.BeginInvoke(new MethodInvoker(delegate { ptProvince.Text = ptAddr["chw_name"].ToString(); }));*/

                string road = "";
                if (!string.IsNullOrEmpty(person.addrRoad))
                {
                    road = person.addrRoad;
                }

                string moo = "";
                if (!string.IsNullOrEmpty(person.addrVillageNo))
                {
                    moo = person.addrVillageNo.Replace("หมู่ที่", "").Trim();
                }

                string tambol = "";
                if (!string.IsNullOrEmpty(person.addrTambol))
                {
                    tambol = person.addrTambol.Replace("ตำบล", "");
                }

                string amphur = "";
                if (!string.IsNullOrEmpty(person.addrAmphur))
                {
                    amphur = person.addrAmphur.Replace("อำเภอ", "");
                }

                string province = "";
                if (!string.IsNullOrEmpty(person.addrProvince))
                {
                    province = person.addrProvince.Replace("จังหวัด", "");
                }

                ptAddress.BeginInvoke(new MethodInvoker(delegate { ptAddress.Text = person.addrHouseNo; }));
                ptRoad.BeginInvoke(new MethodInvoker(delegate { ptRoad.Text = road; }));
                ptMoo.BeginInvoke(new MethodInvoker(delegate { ptMoo.Text = moo; }));

                ptTambol.BeginInvoke(new MethodInvoker(delegate { ptTambol.Text = tambol; }));
                ptAmp.BeginInvoke(new MethodInvoker(delegate { ptAmp.Text = amphur; }));
                ptProvince.BeginInvoke(new MethodInvoker(delegate { ptProvince.Text = province; }));

                IList<JToken> historyItems = historyObj["result"]["vaccine_certificate"].FirstOrDefault()["vaccination_list"].Children().ToList();
                IList<VaccineHistory> historyResults = new List<VaccineHistory>();

                string fullVaccineHistory = "";
                foreach (JToken item in historyItems)
                {
                    VaccineHistory searchResult = item.ToObject<VaccineHistory>();
                    historyResults.Add(searchResult);

                    CultureInfo thInfo = new CultureInfo("th-TH");
                    DateTime dateThai = Convert.ToDateTime(searchResult.vaccine_date, thInfo);

                    var dateThaiFormat = dateThai.ToString("dd MMMM yyyy", thInfo);

                    fullVaccineHistory += "เข็มที่: " + searchResult.vaccine_dose_no + " ";
                    fullVaccineHistory += "วัคซีน: " + searchResult.vaccine_manufacturer_name + " ";
                    fullVaccineHistory += "ฉีดที่: " + searchResult.vaccine_place + " ";
                    fullVaccineHistory += "วันที่: " + dateThaiFormat + "\n";

                }

                Bitmap Photo1 = new Bitmap(person.PhotoBitmap, new Size(160, 200));
                ptImage.Image = Photo1;

                vaccInfo.BeginInvoke(new MethodInvoker(delegate { vaccInfo.Text = fullVaccineHistory; }));

                loadingTxt.BeginInvoke(new MethodInvoker(delegate { loadingTxt.Hide(); }));
                loadingIcon.BeginInvoke(new MethodInvoker(delegate { loadingIcon.Hide(); }));


            }
            else
            {
                loadingTxt.BeginInvoke(new MethodInvoker(delegate { loadingTxt.Hide(); }));
                loadingIcon.BeginInvoke(new MethodInvoker(delegate { loadingIcon.Hide(); }));

                errMsg.BeginInvoke(new MethodInvoker(delegate { errMsg.Text = "ไม่สามารถอ่านข้อมูลบัตรประชาชนได้ ชิปการ์ดอาจมีปัญหา กรุณาเสียบบัตรใหม่อีกครั้ง "; }));
            }

        }
    }

    public class VaccineHistory
    {
        public string vaccine_dose_no { set; get; }
        public string vaccine_date { set; get; }
        public string vaccine_manufacturer_name { set; get; }
        public string vaccine_place { set; get; }
    }
}
