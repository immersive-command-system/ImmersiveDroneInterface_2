/* 
 * This message is auto generated by ROS#. Please DO NOT modify.
 * Note:
 * - Comments from the original code will be written in their own line 
 * - Variable sized arrays will be initialized to array of size 0 
 * Please report any issues at 
 * <https://github.com/siemens/ros-sharp> 
 */



namespace RosSharp.RosBridgeClient.MessageTypes.IsaacsServer
{
    public class ControlDroneRequest : Message
    {
        public const string RosMessageName = "isaacs_server/ControlDrone";

        public uint id { get; set; }
        public string control_task { get; set; }

        public ControlDroneRequest()
        {
            this.id = 0;
            this.control_task = "";
        }

        public ControlDroneRequest(uint id, string control_task)
        {
            this.id = id;
            this.control_task = control_task;
        }
    }
}
