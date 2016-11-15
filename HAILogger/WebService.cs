using HAI_Shared;
using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;

namespace HAILogger
{
    public class WebService
    {
        public static clsHAC HAC;
        WebServiceHost host;

        public WebService(clsHAC hac)
        {
            HAC = hac;
        }

        public void Start()
        {
            Uri uri = new Uri("http://0.0.0.0:" + Global.webapi_port + "/");
            host = new WebServiceHost(typeof(HAIService), uri);

            try
            {
                ServiceEndpoint ep = host.AddServiceEndpoint(typeof(IHAIService), new WebHttpBinding(), "");
                host.Open();

                Event.WriteInfo("WebService", "Listening on " + uri.ToString());
            }
            catch (CommunicationException ex)
            {
                Event.WriteError("WebService", "An exception occurred: " + ex.Message);
                host.Abort();
            }
        }

        public void Stop()
        {
            if (host != null)
                host.Close();
        }
    }
}
