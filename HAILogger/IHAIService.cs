using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace HAILogger
{
    [ServiceContract]
    public interface IHAIService
    {
        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json)]
        void Subscribe(SubscribeContract contract);

        [OperationContract]
        [WebGet(ResponseFormat = WebMessageFormat.Json)]
        List<NameContract> ListAreas();

        [OperationContract]
        [WebGet(ResponseFormat = WebMessageFormat.Json)]
        AreaContract GetArea(ushort id);

        [OperationContract]
        [WebGet(ResponseFormat = WebMessageFormat.Json)]
        List<NameContract> ListZonesContact();

        [OperationContract]
        [WebGet(ResponseFormat = WebMessageFormat.Json)]
        List<NameContract> ListZonesMotion();

        [OperationContract]
        [WebGet(ResponseFormat = WebMessageFormat.Json)]
        List<NameContract> ListZonesWater();

        [OperationContract]
        [WebGet(ResponseFormat = WebMessageFormat.Json)]
        List<NameContract> ListZonesSmoke();

        [OperationContract]
        [WebGet(ResponseFormat = WebMessageFormat.Json)]
        List<NameContract> ListZonesCO();

        [OperationContract]
        [WebGet(ResponseFormat = WebMessageFormat.Json)]
        List<NameContract> ListZonesTemp();

        [OperationContract]
        [WebGet(ResponseFormat = WebMessageFormat.Json)]
        ZoneContract GetZone(ushort id);

        [OperationContract]
        [WebGet(ResponseFormat = WebMessageFormat.Json)]
        List<NameContract> ListUnits();

        [OperationContract]
        [WebGet(ResponseFormat = WebMessageFormat.Json)]
        UnitContract GetUnit(ushort id);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json)]
        void SetUnit(CommandContract unit);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json)]
        void SetUnitKeypadPress(CommandContract unit);

        [OperationContract]
        [WebGet(ResponseFormat = WebMessageFormat.Json)]
        List<NameContract> ListThermostats();

        [OperationContract]
        [WebGet(ResponseFormat = WebMessageFormat.Json)]
        ThermostatContract GetThermostat(ushort id);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json)]
        void SetThermostatCoolSetpoint(CommandContract unit);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json)]
        void SetThermostatHeatSetpoint(CommandContract unit);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json)]
        void SetThermostatMode(CommandContract unit);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json)]
        void SetThermostatFanMode(CommandContract unit);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json)]
        void SetThermostatHold(CommandContract unit);

        [OperationContract]
        [WebGet(ResponseFormat = WebMessageFormat.Json)]
        List<NameContract> ListButtons();

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json)]
        void PushButton(CommandContract unit);
    }
}
