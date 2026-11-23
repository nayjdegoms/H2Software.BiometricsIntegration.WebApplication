
using System;
using System.Collections.Generic;
using System.Net;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Xml;

namespace SmackBio.WebSocketSDK.Sample
{
    public partial class ViewOnlineDevices : System.Web.UI.Page
    {
        public class OnlineDevice
        {
            public string device_uid { get; set; }
            public string session_id { get; set; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                LoadOnlineDevices();
            }
        }

        private void LoadOnlineDevices()
        {
            List<OnlineDevice> devices =
                new List<OnlineDevice>();

            string token =
                System.Configuration.ConfigurationManager
                    .AppSettings["MaintenanceApiToken"];

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException(
                    "Maintenance API token is missing from Web.config.");
            }

            HttpWebRequest request =
                (HttpWebRequest)WebRequest.Create(
                    "http://127.0.0.1:5001/api/device/list");

            request.Method = "GET";
            request.Headers.Add(
                "X-Maintenance-Token", token);

            using (HttpWebResponse response =
                (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            {
                XmlDocument xml = new XmlDocument();
                xml.Load(stream);

                foreach (XmlNode node in
                    xml.SelectNodes(
                        "/MaintenanceResponse/Data/Device"))
                {
                    devices.Add(new OnlineDevice
                    {
                        device_uid = node["device_uid"]?.InnerText,
                        session_id = node["session_id"]?.InnerText
                    });
                }
            }

            online_devices.DataSource = devices;
            online_devices.DataBind();
        }

        protected void online_devices_RowCommand(
            object sender,
            GridViewCommandEventArgs e)
        {
            if (e.CommandName == "Open")
            {
                int index =
                    Convert.ToInt32(e.CommandArgument);

                GridViewRow row = online_devices.Rows[index];

                string sessionId =
                    row.Cells[1].Text;

                string deviceUid =
                    row.Cells[0].Text;

                Context.Response.Redirect(
                    "~/Pages/PageList.aspx?session_id="
                    + HttpUtility.UrlEncode(sessionId)
                    + "&device_uid="
                    + HttpUtility.UrlEncode(deviceUid));
            }
        }

        protected void Refresh_Click(
            object sender,
            EventArgs e)
        {
            LoadOnlineDevices();
        }
    }
}
