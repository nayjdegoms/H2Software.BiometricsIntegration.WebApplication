using SmackBio.WebSocketSDK.Cmd;
using SmackBio.WebSocketSDK.DB;
using SmackBio.WebSocketSDK.M50.Cmd;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Xml;

namespace SmackBio.WebSocketSDK.Sample.Pages
{
    public partial class WiFiSettingPane : System.Web.UI.Page
    {
        private XmlDocument SendMaintenanceCommand(XmlDocument command)
        {
            string token =
                ConfigurationManager.AppSettings["MaintenanceApiToken"];

            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException(
                    "Maintenance API token is not configured.");

            if (command == null || command.DocumentElement == null)
                throw new ArgumentException("Invalid SDK command.");

            // Encode the original SDK command XML.
            string commandBase64 = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(command.OuterXml));

            // Build the request expected by the Windows Service.
            XmlDocument requestXml = new XmlDocument();

            XmlElement root = requestXml.CreateElement("MaintenanceRequest");
            requestXml.AppendChild(root);

            XmlElement deviceIdElement =
                requestXml.CreateElement("DeviceId");

            deviceIdElement.InnerText = device_uid.Text;
            root.AppendChild(deviceIdElement);

            XmlElement commandElement =
                requestXml.CreateElement("CommandXml");

            commandElement.InnerText = commandBase64;
            root.AppendChild(commandElement);

            byte[] requestBytes =
                Encoding.UTF8.GetBytes(requestXml.OuterXml);

            HttpWebRequest request = (HttpWebRequest)
                WebRequest.Create(
                    "http://127.0.0.1:5001/api/device/command");

            request.Method = "POST";
            request.ContentType = "application/xml";
            request.Headers.Add("X-Maintenance-Token", token);
            request.ContentLength = requestBytes.Length;
            request.Timeout = 20000;
            request.ReadWriteTimeout = 20000;

            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(
                    requestBytes, 0, requestBytes.Length);
            }

            using (HttpWebResponse response =
                (HttpWebResponse)request.GetResponse())
            using (Stream responseStream = response.GetResponseStream())
            {
                XmlDocument maintenanceResponse = new XmlDocument();
                maintenanceResponse.Load(responseStream);

                string success =
                    maintenanceResponse.SelectSingleNode(
                        "/MaintenanceResponse/Success")?.InnerText;

                string message =
                    maintenanceResponse.SelectSingleNode(
                        "/MaintenanceResponse/Message")?.InnerText;

                if (!string.Equals(
                    success, "true",
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        message ?? "The maintenance command failed.");
                }

                string responseBase64 =
                    maintenanceResponse.SelectSingleNode(
                        "/MaintenanceResponse/Data")?.InnerText;

                if (string.IsNullOrWhiteSpace(responseBase64))
                    throw new InvalidOperationException(
                        "The service returned an empty command response.");

                // Decode the original SDK response XML.
                string sdkResponseXml = Encoding.UTF8.GetString(
                    Convert.FromBase64String(responseBase64));

                XmlDocument sdkResponse = new XmlDocument();
                sdkResponse.LoadXml(sdkResponseXml);

                return sdkResponse;
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            txtMessage.Text = "";
            error_message.Text = "";

            var sid = Context.Request.Params["session_id"];
            var dev_uid = Context.Request.Params["device_uid"];
            if (!string.IsNullOrEmpty(sid))
            {
                session_id.Text = sid;
                device_uid.Text = dev_uid;
            }
            else
                Context.Response.Redirect("~/ViewOnlineDevices.aspx");
        }

        protected void btnBack_Click(object sender, EventArgs e)
        {
            Context.Response.Redirect(
                    "~/Pages/PageList.aspx?session_id="
                    + HttpUtility.UrlEncode(session_id.Text)
                    + "&device_uid="
                    + HttpUtility.UrlEncode(device_uid.Text));
        }

        protected void btnGetWiFiSetting_Click(object sender, EventArgs e)
        {
            CmdGetWiFiSetting cmd = new CmdGetWiFiSetting();
            try
            {
                //var session = SessionRegistry.GetSession(Guid.Parse(session_id.Text));

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(cmd.Build());

                // Send the command through the Windows Service.
                XmlDocument response = SendMaintenanceCommand(doc);

                CmdGetWiFiSettingResponse cmd_resp =
                    new CmdGetWiFiSettingResponse();

                if (cmd_resp.Parse(response))
                {
                    chkUseWifi.Checked = cmd_resp.setting.use_wifi;
                    txtSSID.Text = cmd_resp.setting.SSID;
                    txtKey.Text = cmd_resp.setting.key;
                    chkUseDHCP.Checked = cmd_resp.setting.use_dhcp;
                    txtIP.Text = cmd_resp.setting.ip;
                    txtSubnetMask.Text = cmd_resp.setting.subnet;
                    txtGateway.Text = cmd_resp.setting.gateway;
                    txtPort.Text = cmd_resp.setting.port.ToString();

                    txtIP_from_dhcp.Text = cmd_resp.setting.ip_from_dhcp;
                    txtSubnetMask_from_dhcp.Text = cmd_resp.setting.subnet_from_dhcp;
                    txtGateway_from_dhcp.Text = cmd_resp.setting.gateway_from_dhcp;

                    txtMessage.Text = "Get WiFiSetting OK";
                }
                else
                    txtMessage.Text = "Get WiFiSetting Failed";
            }
            catch (Exception ex)
            {
                error_message.Text = ex.Message;
            }
        }
        
        protected void btnSetWiFiSetting_Click(object sender, EventArgs e)
        {
            try
            {
                WiFiSetting wifi_setting = new WiFiSetting();

                wifi_setting.use_wifi = chkUseWifi.Checked;
                wifi_setting.SSID = txtSSID.Text;
                wifi_setting.key = txtKey.Text;
                wifi_setting.use_dhcp = chkUseDHCP.Checked;
                wifi_setting.ip = txtIP.Text;
                wifi_setting.subnet = txtSubnetMask.Text;
                wifi_setting.gateway = txtGateway.Text;
                wifi_setting.port = Convert.ToInt32(txtPort.Text);

                CmdSetWiFi cmd = new CmdSetWiFi(wifi_setting);

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(cmd.Build());

                XmlDocument response = SendMaintenanceCommand(doc);

                txtMessage.Text = "Set WiFi Failed.";

                if (BaseMessage.IsResponseKey(response, CmdSetWiFi.MSG_KEY))
                {
                    GeneralResponse res = new GeneralResponse();

                    if (res.ParseResult(response) == CommandExeResult.OK)
                    {
                        txtMessage.Text = "SetWiFi OK!";
                    }
                }
            }
            catch (Exception ex)
            {
                error_message.Text = ex.Message;
            }
        }
    }
}