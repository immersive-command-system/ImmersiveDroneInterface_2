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
    public class TopicTypes : Message
    {
        public const string RosMessageName = "isaacs_server/TopicTypes";

        public string name { get; set; }
        public string type { get; set; }

        public TopicTypes()
        {
            this.name = "";
            this.type = "";
        }

        public TopicTypes(string name, string type)
        {
            this.name = name;
            this.type = type;
        }
    }
}
