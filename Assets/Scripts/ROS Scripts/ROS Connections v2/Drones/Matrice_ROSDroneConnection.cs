﻿using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;

using ROSBridgeLib;
using ROSBridgeLib.geometry_msgs;
using ROSBridgeLib.sensor_msgs;
using ROSBridgeLib.std_msgs;
using ROSBridgeLib.interface_msgs;

using ISAACS;

public class Matrice_ROSDroneConnection : MonoBehaviour, ROSTopicSubscriber, ROSDroneConnectionInterface
{
    /// <para>
    /// Drone state variables and helper enums
    /// </para>    

    /// <summary>
    /// Current flight status of the drone
    /// </summary>
    public enum FlightStatus
    {
        ON_GROUND_STANDBY = 1,
        TAKEOFF = 2,
        IN_AIR_STANDBY = 3,
        LANDING = 4,
        FINISHING_LANDING = 5
    }

    /// <summary>
    /// Possible camera actions that can be executed
    /// </summary>
    public enum CameraAction
    {
        SHOOT_PHOTO = 0,
        START_VIDEO = 1,
        STOP_VIDEO = 2
    }

    /// <summary>
    /// Possible drone tasks that can be executed
    /// </summary>
    public enum DroneTask
    {
        GO_HOME = 1,
        TAKEOFF = 4,
        LAND = 6
    }

    /// <summary>
    /// Possible action commands during a waypoint mission
    /// </summary>
    public enum WaypointMissionAction
    {
        START = 0,
        STOP = 1,
        PAUSE = 2,
        RESUME = 3
    }

    /// <summary>
    /// ROS connection variable to the drone
    /// </summary>
    private ROSBridgeWebSocketConnection ros = null;
    /// <summary>
    /// Unique drone identifier
    /// </summary>
    string client_id;
    /// <summary>
    /// Status of drone simulator, initilized by user in editor
    /// </summary>
    bool simDrone = false;

    /// <summary>
    /// DJI SDK status
    /// </summary>
    public bool sdk_ready
    {
        get
        {
            return ros != null;
        }
    }

    /// <summary>
    /// Status of ability to control drone via Unity ROS connection
    /// True: Commands send from Unity will be executed
    /// False: Commands send from Unity will be rejected as drone control authority is with another controller/operator
    /// </summary>
    bool has_authority = false;

    /// <summary>
    /// Battery state of the drone
    /// </summary>
    BatteryStateMsg battery_state;
    
    /// <summary>
    /// Current flight status of the drone
    /// </summary>
    FlightStatus flight_status;

    /// <summary>
    /// Reading of the 6 channels of the remote controller, published at 50 Hz.
    /// </summary>
    JoyMsg remote_controller_msg;

    /// <summary>
    /// Vehicle attitude is as quaternion for the rotation from Forward-Left-Up (FLU) body frame to East-North-Up (ENU) ground frame, published at 100 Hz.
    /// </summary>
    Quaternion attitude = Quaternion.identity;

    /// <summary>
    /// Offset used to convert drone attitude to Unity axis.
    /// </summary>
    Quaternion offset = Quaternion.Euler(90, 180, 0);

    /// <summary>
    /// IMU data including raw gyro reading in FLU body frame, raw accelerometer reading in FLU body frame, and attitude estimation, 
    /// published at 100 Hz for M100, and 400 Hz for other platforms. 
    /// Note that raw accelerometer reading will give a Z direction 9.8 m/s2 when the drone is put on a level ground statically.
    /// </summary>
    IMUMsg imu;
    
    /// <summary>
    /// Current velocity of the drone
    /// </summary>
    Vector3 velocity;

    /// <summary>
    /// Height above takeoff location. It is only valid after drone is armed, when the flight controller has a reference altitude set.
    /// </summary>
    float relative_altitude;

    /// <summary>
    /// Local position in Cartesian ENU frame, of which the origin is set by the user by calling the /dji_sdk/set_local_pos_ref service. 
    /// Note that the local position is calculated from GPS position, so good GPS health is needed for the local position to be useful.
    /// </summary>
    Vector3 local_position;
    
    /// <summary>
    /// Current angles of gimbal
    /// </summary>
    Vector3 gimbal_joint_angles;
    
    /// <summary>
    /// Current gps health
    /// </summary>
    uint gps_health;
    
    /// <summary>
    /// Current gps position
    /// </summary>
    NavSatFixMsg gps_position;
    
    /// <summary>
    /// The latitude of the starting point of the drone flight
    /// </summary>
    double droneHomeLat = 0;
    
    /// <summary>
    /// The longitude of the starting point of the drone flight
    /// </summary>
    double droneHomeLong = 0;

    /// <summary>
    /// Initilize drone home position if it hasn't been set yet
    /// </summary>
    bool droneHomeSet = false;

    /// <summary>
    /// Function called by ROSManager when Drone Gameobject is initilized to start the ROS connection with requested subscribers.
    /// </summary>
    /// <param name="uniqueID"> Unique identifier</param>
    /// <param name="droneIP"> Drone IP address for ROS connection</param>
    /// <param name="dronePort"> Drone Port value for ROS connection</param>
    /// <param name="droneSubscribers"> List of subscibers to connect to and display in the informative UI</param>
    /// <param name="simFlight"> Boolean value to active or deactive DroneFlightSim</param>
    public void InitilizeDrone(int uniqueID, string droneIP, int dronePort, List<string> droneSubscribers, bool simFlight)
    {
        ros = new ROSBridgeWebSocketConnection("ws://" + droneIP, dronePort);
        client_id = uniqueID.ToString();
        simDrone = simFlight;

        foreach (string subscriber in droneSubscribers)
        {
            ros.AddSubscriber("/dji_sdk/" + subscriber, this);
        }

        // TODO: Initilize Informative UI Prefab and attach as child.
        ros.Connect();
    }

    // Update is called once per frame in Unity
    void Update()
    {
        if (ros != null)
        {
            ros.Render();
        }
    }

    /// <summary>
    /// The Control UI should call this function to start the mission
    /// Start the waypoint mission.
    /// </summary>
    public void StartMission()
    {
        // Integrate dynamic waypoint system

        if (simDrone)
        {
            this.GetComponent<DroneSimulationManager>().FlyNextWaypoint();
            return;
        }

        List<MissionWaypointMsg> missionMsgList = new List<MissionWaypointMsg>();

        uint[] command_list = new uint[16];
        uint[] command_params = new uint[16];

        for (int i = 0; i < 16; i++)
        {
            command_list[i] = 0;
            command_params[i] = 0;
        }

        ArrayList waypoints = new ArrayList(this.GetComponent<DroneProperties>().classPointer.waypoints);

        // Removing first waypoint set above the drone as takeoff is automatic.
        waypoints.RemoveAt(0);

        foreach (Waypoint waypoint in waypoints)
        {
            float x = waypoint.gameObjectPointer.transform.localPosition.x;
            float y = waypoint.gameObjectPointer.transform.localPosition.y;
            float z = waypoint.gameObjectPointer.transform.localPosition.z;

            double ROS_lat = WorldProperties.UnityXToLat(this.droneHomeLat, x);
            // TODO: Clean hardcoded quanities in Unity - ROS Coordinates clean up
            float ROS_alt = (y * WorldProperties.Unity_Y_To_Alt_Scale) - 1f;
            double ROS_long = WorldProperties.UnityZToLong(this.droneHomeLong, this.droneHomeLat, z);

            MissionWaypointMsg new_waypoint = new MissionWaypointMsg(ROS_lat, ROS_long, ROS_alt, 3.0f, 0, 0, MissionWaypointMsg.TurnMode.CLOCKWISE, 0, 30, new MissionWaypointActionMsg(0, command_list, command_params));
            Debug.Log("Adding waypoint at: " + new_waypoint);
            missionMsgList.Add(new_waypoint);
        }
        MissionWaypointTaskMsg Task = new MissionWaypointTaskMsg(15.0f, 15.0f, MissionWaypointTaskMsg.ActionOnFinish.AUTO_LANDING, 1, MissionWaypointTaskMsg.YawMode.AUTO, MissionWaypointTaskMsg.TraceMode.POINT, MissionWaypointTaskMsg.ActionOnRCLost.FREE, MissionWaypointTaskMsg.GimbalPitchMode.FREE, missionMsgList.ToArray());
        Debug.Log("Uploading waypoint mission");
        UploadWaypointsTask(Task);
    }

    /// <summary>
    /// The Control UI should call this function to pause mission.
    /// Pause an active mission.
    /// </summary>
    public void PauseMission()
    {
        if (simDrone)
        {
            this.GetComponent<DroneSimulationManager>().pauseFlight();
            return;
        }

        SendWaypointAction(WaypointMissionAction.PAUSE);
    }

    /// <summary>
    /// The Control UI should call this function to resume mission
    /// Resume a paused mission.
    /// </summary>
    public void ResumeMission()
    {
        if (simDrone)
        {
            this.GetComponent<DroneSimulationManager>().resumeFlight();
            return;
        }

        SendWaypointAction(WaypointMissionAction.RESUME);
    }
    
    /// <summary>
    /// The Control UI should call this function to update mission
    /// Update the waypoint mission.
    /// </summary>
    public void UpdateMission()
    {
        if (simDrone)
        {
            this.GetComponent<DroneSimulationManager>().FlyNextWaypoint(true);
            return;
        }

        // TODO: Integrate dynamic waypoint system
        SendWaypointAction(WaypointMissionAction.STOP);
        StartMission();
    }
    
    /// <summary>
    /// The Control UI should call this function to land the drone
    /// Land the drone at the current position.
    /// </summary>
    public void LandDrone()
    {
        if (simDrone)
        {
            this.GetComponent<DroneSimulationManager>().flyHome();
            return;
        }

        ExecuteTask(DroneTask.LAND);
    }
    
    /// <summary>
    /// The Control UI should call this function to fly the drone home
    /// Command the drone to fly back to the home position.
    /// </summary>
    public void FlyHome()
    {
        if (simDrone)
        {
            this.GetComponent<DroneSimulationManager>().flyHome();
            return;
        }

        ExecuteTask(DroneTask.GO_HOME);
    }

    /// <para>
    /// Public methods to query state variables of the drone
    /// The Informative UI should only query these methods
    /// </para>

    /// <summary>
    /// State of control authority Unity interface has over drone
    /// </summary>
    public bool HasAuthority()
    {
        return has_authority;
    }
    
    /// <summary>
    /// Current drone flight status
    /// </summary>
    /// <returns></returns>
    public FlightStatus GetFlightStatus()
    {
        return flight_status;
    }

    /// <summary>
    /// Current attitude of the drone
    /// </summary>
    /// <returns></returns>
    public Quaternion GetAttitude()
    {
        return attitude;
    }

    /// <summary>
    /// Current GPS Position of the drone
    /// </summary>
    /// <returns></returns>
    public NavSatFixMsg GetGPSPosition()
    {
        return gps_position;
    }

    /// <summary>
    /// Current height of drone relative to take off height
    /// </summary>
    /// <returns></returns>
    public float GetHeightAboveTakeoff()
    {
        return relative_altitude;
    }

    /// <summary>
    /// Position of drone relative to set Local Position
    /// Not valid if Local Position has not been set
    /// </summary>
    /// <returns></returns>
    public Vector3 GetLocalPosition()
    {
        return local_position;
    }

    /// <summary>
    /// Current velocity of the drone
    /// </summary>
    /// <returns></returns>
    public Vector3 GetVelocity()
    {
        return velocity;
    }

    /// <summary>
    /// Current angles of attached gimbal
    /// </summary>
    /// <returns></returns>
    public Vector3 GetGimbalJointAngles()
    {
        return gimbal_joint_angles;
    }

    /// <summary>
    /// Strength of GPS connection
    /// </summary>
    /// <returns></returns>
    public float GetGPSHealth()
    {
        return gps_health;
    }

    /// <summary>
    /// Home Latitude of the drone
    /// </summary>
    /// <returns></returns>
    public double GetHomeLat()
    {
        return droneHomeLat;
    }

    /// <summary>
    /// Home Longitude of the drone
    /// </summary>
    /// <returns></returns>
    public double GetHomeLong()
    {
        return droneHomeLong;
    }

    /// ROSTopicSubscriber Interface methods

    /// <summary>
    /// Parse received message from drone based on topic and perform required action
    /// </summary>
    /// <param name="topic"></param>
    /// <param name="raw_msg"></param>
    /// <param name="parsed"></param>
    /// <returns></returns>
    public ROSBridgeMsg OnReceiveMessage(string topic, JSONNode raw_msg, ROSBridgeMsg parsed = null)
    {
        ROSBridgeMsg result = null;
        // Writing all code in here for now. May need to move out to separate handler functions when it gets too unwieldy.
        switch (topic)
        {
            case "/dji_sdk/attitude":
                QuaternionMsg attitudeMsg = (parsed == null) ? new QuaternionMsg(raw_msg["quaternion"]) : (QuaternionMsg)parsed;
                attitude = offset * (new Quaternion(attitudeMsg.GetX(), attitudeMsg.GetY(), attitudeMsg.GetZ(), attitudeMsg.GetW()));
                result = attitudeMsg;
                break;
            case "/dji_sdk/battery_state":
                battery_state = (parsed == null) ? new BatteryStateMsg(raw_msg) : (BatteryStateMsg)parsed;
                result = battery_state;
                break;
            case "/dji_sdk/flight_status":
                flight_status = (FlightStatus)(new UInt8Msg(raw_msg)).GetData();
                break;
            case "/dji_sdk/gimbal_angle":
                Vector3Msg gimbalAngleMsg = (parsed == null) ? new Vector3Msg(raw_msg["vector"]) : (Vector3Msg)parsed;
                gimbal_joint_angles = new Vector3((float)gimbalAngleMsg.GetX(), (float)gimbalAngleMsg.GetY(), (float)gimbalAngleMsg.GetZ());
                result = gimbalAngleMsg;
                break;
            case "/dji_sdk/gps_health":
                gps_health = (parsed == null) ? (new UInt8Msg(raw_msg)).GetData() : ((UInt8Msg)parsed).GetData();
                break;
            case "/dji_sdk/gps_position":
                gps_position = (parsed == null) ? new NavSatFixMsg(raw_msg) : (NavSatFixMsg)parsed;
                result = gps_position;

                double droneLat = gps_position.GetLatitude();
                double droneLong = gps_position.GetLongitude();

                // TODO: Test that setting drone home latitude and longitutde as first message from drone gps position works.
                if (droneHomeSet == false)
                {
                    droneHomeLat = droneLat;
                    droneHomeLong = droneLong;
                    droneHomeSet = true;
                }

                // TODO: Complete function in World properties.
                if (droneHomeSet)
                {
                    this.transform.localPosition = WorldProperties.ROSCoordToUnityCoord(gps_position);
                }

                break;
            case "/dji_sdk/imu":
                imu = (parsed == null) ? new IMUMsg(raw_msg) : (IMUMsg)parsed;
                result = imu;
                break;
            case "/dji_sdk/rc":
                remote_controller_msg = (parsed == null) ? new JoyMsg(raw_msg) : (JoyMsg)parsed;
                result = remote_controller_msg;
                break;
            case "/dji_sdk/velocity":
                Vector3Msg velocityMsg = (parsed == null) ? new Vector3Msg(raw_msg["vector"]) : (Vector3Msg)parsed;
                velocity = new Vector3((float)velocityMsg.GetX(), (float)velocityMsg.GetY(), (float)velocityMsg.GetZ());
                result = velocityMsg;
                break;
            case "/dji_sdk/height_above_takeoff":
                relative_altitude = (parsed == null) ? (new Float32Msg(raw_msg)).GetData() : ((Float32Msg)parsed).GetData();
                break;
            case "/dji_sdk/local_position":
                PointMsg pointMsg = (parsed == null) ? new PointMsg(raw_msg["point"]) : (PointMsg)parsed;
                local_position = new Vector3(pointMsg.GetX(), pointMsg.GetY(), pointMsg.GetZ());
                result = pointMsg;
                Debug.Log(result);
                break;
            default:
                Debug.LogError("Topic not implemented: " + topic);
                break;
        }
        return result;
    }

    /// <summary>
    /// Get ROS message type for a valid topic.
    /// </summary>
    /// <param name="topic"></param>
    /// <returns></returns>
    public string GetMessageType(string topic)
    {
        switch (topic)
        {
            case "/dji_sdk/attitude":
                return "geometry_msgs/QuaternionStamped";
            case "/dji_sdk/battery_state":
                return "sensor_msgs/BatteryState";
            case "/dji_sdk/flight_status":
                return "std_msgs/UInt8";
            case "/dji_sdk/gimbal_angle":
                return "geometry_msgs/Vector3Stamped";
            case "/dji_sdk/gps_health":
                return "std_msgs/UInt8";
            case "/dji_sdk/gps_position":
                return "sensor_msgs/NavSatFix";
            case "/dji_sdk/imu":
                return "sensor_msgs/Imu";
            case "/dji_sdk/rc":
                return "sensor_msgs/Joy";
            case "/dji_sdk/velocity":
                return "geometry_msgs/Vector3Stamped";
            case "/dji_sdk/height_above_takeoff":
                return "std_msgs/Float32";
            case "/dji_sdk/local_position":
                return "geometry_msgs/PointStamped";
        }
        Debug.LogError("Topic " + topic + " not registered.");
        return "";
    }

    /// <summary>
    /// Disconnect the ros connection, terminated via ROSManager
    /// </summary>
    public void DisconnectROSConnection()
    {
        ros.Disconnect();
    }


    /// <para>
    /// Methods to execute service calls to the DJI SDK onboard the drone and corresponding methods to handle DJI SDK response
    /// All responses are currently printed out to the console. 
    /// Logical code implementation will be build as required.
    /// </para>

    /// <summary>
    /// Query drone version
    /// </summary>
    public void FetchDroneVersion()
    {
        string service_name = "dji_sdk/query_drone_version";
        Debug.LogFormat("ROS Call: {0} {1}", client_id, service_name);
        ros.CallService(HandleDroneVersionResponse, service_name, string.Format("{0} {1}", client_id, service_name));
    }
    /// <summary>
    /// Parse drone query response.
    /// </summary>
    /// <param name="response"></param>
    public void HandleDroneVersionResponse(JSONNode response)
    {
        response = response["values"];
        Debug.LogFormat("Drone: {0} (Version {1})", response["hardware"].Value, response["version"].AsInt);
    }

    /// <summary>
    /// Activate drone
    /// </summary>
    public void ActivateDrone()
    {
        string service_name = "/dji_sdk/activation";
        Debug.LogFormat("ROS Call: {0} {1}", client_id, service_name);
        ros.CallService(HandleActivationResponse, service_name, string.Format("{0} {1}", client_id, service_name));
    }
    /// <summary>
    /// Parse drone activation response.
    /// </summary>
    /// <param name="response"></param>
    public void HandleActivationResponse(JSONNode response)
    {
        response = response["values"];
        Debug.LogFormat("Activation {0} (ACK: {1})", (response["result"].AsBool ? "succeeded" : "failed"), response["ack_data"].AsInt);
    }

    /// <summary>
    /// Obtain or relinquish control over drone
    /// </summary>
    /// <param name="control"></param>
    public void SetSDKControl(bool control)
    {
        string service_name = "/dji_sdk/sdk_control_authority";
        Debug.LogFormat("ROS Call: {0} {1}  Arguments: {2}", client_id, service_name, control);
        ros.CallService(HandleSetSDKControlResponse, service_name, string.Format("{0} {1}", client_id, service_name), string.Format("[{0}]", (control ? 1 : 0)));
        has_authority = control;
    }
    /// <summary>
    /// Parse SDK control response
    /// </summary>
    /// <param name="response"></param>
    public void HandleSetSDKControlResponse(JSONNode response)
    {
        response = response["values"];
        Debug.Log(response.ToString());
        Debug.LogFormat("Control request {0} (ACK: {1})", (response["result"].AsBool ? "succeeded" : "failed"), response["ack_data"].AsInt);
        //if (response["result"].AsBool == true)
        //{
        //    has_authority = requested_authority;
        //}
    }

    /// <summary>
    /// Command a drone arm to perform a task
    /// </summary>
    /// <param name="armed"></param>
    public void ChangeArmStatusTo(bool armed)
    {
        string service_name = "/dji_sdk/drone_arm_control";
        Debug.LogFormat("ROS Call: {0} {1}  Arguments: {2}", client_id, service_name, armed);
        ros.CallService(HandleArmResponse, service_name, string.Format("{0} {1}", client_id, service_name), string.Format("[{0}]", (armed ? 1 : 0)));
    }
    /// <summary>
    /// Parse drone arm status response
    /// </summary>
    /// <param name="response"></param>
    public void HandleArmResponse(JSONNode response)
    {
        response = response["values"];
        Debug.LogFormat("Arm/Disarm request {0} (ACK: {1})", (response["result"].AsBool ? "succeeded" : "failed"), response["ack_data"].AsInt);
    }

    /// <summary>
    /// Command drone to execute task
    /// </summary>
    /// <param name="task"></param>
    public void ExecuteTask(DroneTask task)
    {
        string service_name = "/dji_sdk/drone_task_control";
        Debug.LogFormat("ROS Call: {0} {1}  Arguments: {2}", client_id, service_name, task);
        ros.CallService(HandleTaskResponse, service_name, string.Format("{0} {1}", client_id, service_name), string.Format("[{0}]", (int)task));
    }
    /// <summary>
    /// Parse drone task command response
    /// </summary>
    /// <param name="response"></param>
    public void HandleTaskResponse(JSONNode response)
    {
        response = response["values"];
        Debug.LogFormat("Task request {0} (ACK: {1})", (response["result"].AsBool ? "succeeded" : "failed"), response["ack_data"].AsInt);
    }

    /// <summary>
    /// Set Local Position origion of the drone
    /// </summary>
    public void SetLocalPosOriginToCurrentLocation()
    {
        string service_name = "/dji_sdk/set_local_pos_ref";
        Debug.LogFormat("ROS Call: {0} {1}", client_id, service_name);
        ros.CallService(HandleSetLocalPosOriginResponse, service_name, string.Format("{0} {1}", client_id, service_name));
    }
    /// <summary>
    /// Parse response of setting local drone position
    /// </summary>
    /// <param name="response"></param>
    public void HandleSetLocalPosOriginResponse(JSONNode response)
    {
        response = response["values"];
        Debug.LogFormat("Local position origin set {0}", (response["result"].AsBool ? "succeeded" : "failed"));
    }

    /// <summary>
    /// Execute camera action
    /// </summary>
    /// <param name="action"></param>
    public void ExecuteCameraAction(CameraAction action)
    {
        string service_name = "/dji_sdk/camera_action";
        Debug.LogFormat("ROS Call: {0} {1}  Arguments: {2}", client_id, service_name, action);
        ros.CallService(HandleCameraActionResponse, service_name, string.Format("{0} {1}", client_id, service_name), args: string.Format("[{0}]", (int)action));
    }
    /// <summary>
    /// Parse response of executing camera action
    /// </summary>
    /// <param name="response"></param>
    public void HandleCameraActionResponse(JSONNode response)
    {
        response = response["values"];
        Debug.LogFormat("Camera action {0}", (response["result"].AsBool ? "succeeded" : "failed"));
    }

    /// <summary>
    /// Query current mission status
    /// </summary>
    public void FetchMissionStatus()
    {
        string service_name = "/dji_sdk/mission_status";
        Debug.LogFormat("ROS Call: {0} {1} ", client_id, service_name);
        ros.CallService(HandleMissionStatusResponse, service_name, string.Format("{0} {1}", client_id, service_name));
    }
    /// <summary>
    /// Parse mission status query response
    /// </summary>
    /// <param name="response"></param>
    public void HandleMissionStatusResponse(JSONNode response)
    {
        response = response["values"];
        Debug.LogFormat("Waypoint Count: {0}\nHotpoint Count: {1}", response["waypoint_mission_count"], response["hotpoint_mission_count"]);
    }

    /// <summary>
    /// Upload waypoint mission task
    /// </summary>
    /// <param name="task"></param>
    public void UploadWaypointsTask(MissionWaypointTaskMsg task)
    {
        string service_name = "/dji_sdk/mission_waypoint_upload";
        Debug.LogFormat("ROS Call: {0} {1}  Arguments: {2}", client_id, service_name, task);
        ros.CallService(HandleUploadWaypointsTaskResponse, service_name, string.Format("{0} {1}", client_id, service_name), args: string.Format("[{0}]", task.ToYAMLString()));
    }
    /// <summary>
    /// Parse waypoint mission taks upload response
    /// </summary>
    /// <param name="response"></param>
    public void HandleUploadWaypointsTaskResponse(JSONNode response)
    {
        response = response["values"];
        Debug.LogFormat("Waypoint task upload {0} (ACK: {1})", (response["result"].AsBool ? "succeeded" : "failed"), response["ack_data"].AsInt);

        // Start flight upon completing upload
        // Disabled for now
        /*
        if (response["result"].AsBool == true)
        {
            SendWaypointAction(WaypointMissionAction.START);
        }
        else
        {
            StartMission();
        }
        */
    }

    /// <summary>
    /// Send waypoint action command
    /// </summary>
    /// <param name="action"></param>
    public void SendWaypointAction(WaypointMissionAction action)
    {
        string service_name = "/dji_sdk/mission_waypoint_action";
        Debug.LogFormat("ROS Call: {0} {1}  Arguments: {2}", client_id, service_name, action);
        ros.CallService(HandleWaypointActionResponse, service_name, string.Format("{0} {1}", client_id, service_name), args: string.Format("[{0}]", (int)action));
    }
    /// <summary>
    /// Parse response to sent waypoint action command
    /// </summary>
    /// <param name="response"></param>
    public void HandleWaypointActionResponse(JSONNode response)
    {
        response = response["values"];
        Debug.LogFormat("Waypoint action {0} (ACK: {1})", (response["result"].AsBool ? "succeeded" : "failed"), response["ack_data"].AsInt);
    }

    /// <summary>
    /// Query current waypoint mission
    /// </summary>
    public void FetchCurrentWaypointMission()
    {
        string service_name = "/dji_sdk/mission_waypoint_getInfo";
        Debug.LogFormat("ROS Call: {0} {1}", client_id, service_name);
        ros.CallService(HandleCurrentWaypointMissionResponse, service_name, string.Format("{0} {1}", client_id, service_name));
    }
    /// <summary>
    /// Parse current waypoint mission query response
    /// </summary>
    /// <param name="response"></param>
    public void HandleCurrentWaypointMissionResponse(JSONNode response)
    {
        MissionWaypointTaskMsg waypoint_task = new MissionWaypointTaskMsg(response["values"]);
        Debug.LogFormat("Current waypoint mission: \n{0}", waypoint_task.ToYAMLString());
    }

    /// <summary>
    /// Query waypoint velocity
    /// </summary>
    public void FetchWaypointSpeed()
    {
        string service_name = "/dji_sdk/mission_waypoint_getSpeed";
        Debug.LogFormat("ROS Call: {0} {1}", client_id, service_name);
        ros.CallService(HandleWaypointSpeedResponse, service_name, string.Format("{0} {1}", client_id, service_name));
    }
    /// <summary>
    /// Parse waypoint velocity query response
    /// </summary>
    /// <param name="response"></param>
    public void HandleWaypointSpeedResponse(JSONNode response)
    {
        response = response["values"];
        Debug.LogFormat("Current waypoint speed: {0}", response["speed"].AsFloat);
    }

    /// <summary>
    /// Set waypoint mission velocity
    /// </summary>
    /// <param name="speed"></param>
    public void SetWaypointSpeed(float speed)
    {
        string service_name = "/dji_sdk/mission_waypoint_setSpeed";
        Debug.LogFormat("ROS Call: {0} {1}  Arguments: {2}", client_id, service_name, speed);
        ros.CallService(HandleSetWaypointSpeedResponse, service_name, string.Format("{0} {1}", client_id, service_name), args: string.Format("[{0}]", speed));
    }
    /// <summary>
    /// Parse response of setting waypoint mission velocity command
    /// </summary>
    /// <param name="response"></param>
    public void HandleSetWaypointSpeedResponse(JSONNode response)
    {
        response = response["values"];
        Debug.LogFormat("Set waypoint speed {0} (ACK: {1})", (response["result"].AsBool ? "succeeded" : "failed"), response["ack_data"].AsInt);
    }

    /// <summary>
    /// Subscribe to specified 240p camera's
    /// </summary>
    /// <param name="front_right"></param>
    /// <param name="front_left"></param>
    /// <param name="down_front"></param>
    /// <param name="down_back"></param>
    public void Subscribe240p(bool front_right, bool front_left, bool down_front, bool down_back)
    {
        string serviceName = "/dji_sdk/stereo_240p_subscription";
        string id = string.Format("{0} {1} subscribe", client_id, serviceName);
        string args = string.Format("[{0} {1} {2} {3} 0]", front_right ? 1 : 0, front_left ? 1 : 0, down_front ? 1 : 0, down_back ? 1 : 0);
        ros.CallService(HandleSubscribe240pResponse, serviceName, id, args);
    }
    /// <summary>
    /// Parse camera stream responses
    /// </summary>
    /// <param name="response"></param>
    public void HandleSubscribe240pResponse(JSONNode response)
    {
        response = response["values"];
        Debug.Log("Subscribe to 240p feeds " + ((response["result"].AsBool) ? "succeeded" : "failed"));
    }

    /// <summary>
    /// Unsubscribe from camera stream
    /// </summary>
    public void Unsubscribe240p()
    {
        string serviceName = "/dji_sdk/stereo_240p_subscription";
        string id = string.Format("{0} {1} unsubscribe", client_id, serviceName);
        ros.CallService(HandleUnsubscribe240pResponse, serviceName, id, "[0 0 0 0 1]");
    }
    /// <summary>
    /// Parse response for unsubscription from camera stream request
    /// </summary>
    /// <param name="response"></param>
    public void HandleUnsubscribe240pResponse(JSONNode response)
    {
        response = response["values"];
        Debug.Log("Unsubscribe to 240p feeds " + ((response["result"].AsBool) ? "succeeded" : "failed"));
    }

    /// <summary>
    /// Subscribe to front depth camera
    /// </summary>
    public void SubscribeDepthFront()
    {
        string serviceName = "/dji_sdk/stereo_depth_subscription";
        string id = string.Format("{0} {1} subscribe", client_id, serviceName);
        ros.CallService(HandleSubscribeDepthFrontResponse, serviceName, id, "[1 0]");
    }
    /// <summary>
    /// Parse response to front depth camera subscription command
    /// </summary>
    /// <param name="response"></param>
    public void HandleSubscribeDepthFrontResponse(JSONNode response)
    {
        response = response["values"];
        Debug.Log("Subscribe front depth feed " + ((response["result"].AsBool) ? "succeeded" : "failed"));
    }

    /// <summary>
    /// Unsubscribe to front depth camera
    /// </summary>
    public void UnsubscribeDepthFront()
    {
        string serviceName = "/dji_sdk/stereo_depth_subscription";
        string id = string.Format("{0} {1} unsubscribe", client_id, serviceName);
        ros.CallService(HandleUnsubscribeDepthFrontResponse, serviceName, id, "[0 1]");
    }
    /// <summary>
    /// Parse response to front depth camera unsubscription command
    /// </summary>
    /// <param name="response"></param>
    public void HandleUnsubscribeDepthFrontResponse(JSONNode response)
    {
        response = response["values"];
        Debug.Log("Unsubscribe front depth feed " + ((response["result"].AsBool) ? "succeeded" : "failed"));
    }

    /// <summary>
    /// Subscribe to front VGA camera
    /// </summary>
    public void SubscribeVGAFront(bool use_20Hz)
    {
        string serviceName = "/dji_sdk/stereo_vga_subscription";
        string id = string.Format("{0} {1} subscribe", client_id, serviceName);
        ros.CallService(HandleSubscribeVGAFrontResponse, serviceName, id, string.Format("[{0} 1 0]", use_20Hz ? 0 : 1));
    }
    /// <summary>
    /// Parse response to front VGA camera subscription command
    /// </summary>
    /// <param name="response"></param>
    public void HandleSubscribeVGAFrontResponse(JSONNode response)
    {
        response = response["values"];
        Debug.Log("Subscribe VGA front feed " + ((response["result"].AsBool) ? "succeeded" : "failed"));
    }

    /// <summary>
    /// Unsubscribe to front VGA camera
    /// </summary>
    public void UnsubscribeVGAFront()
    {
        string serviceName = "/dji_sdk/stereo_vga_subscription";
        string id = string.Format("{0} {1} unsubscribe", client_id, serviceName);
        ros.CallService(HandleUnsubscribeVGAFrontResponse, serviceName, id, "[0 0 1]");
    }
    /// <summary>
    /// Parse response to front VGA camera unsubscription command
    /// </summary>
    /// <param name="response"></param>
    public void HandleUnsubscribeVGAFrontResponse(JSONNode response)
    {
        response = response["values"];
        Debug.Log("Unsubscribe VGA front feed " + ((response["result"].AsBool) ? "succeeded" : "failed"));
    }

    /// <summary>
    /// Subscribe to FPV camera
    /// </summary>
    public void SubscribeFPV()
    {
        string serviceName = "/dji_sdk/setup_camera_stream";
        string id = string.Format("{0} {1} subscribe FPV", client_id, serviceName);
        ros.CallService(HandleSubscribeFPVResponse, serviceName, id, "[0 1]");
    }
    /// <summary>
    /// Parse response to FPV camera subscription command
    /// </summary>
    /// <param name="response"></param>
    public void HandleSubscribeFPVResponse(JSONNode response)
    {
        response = response["values"];
        Debug.Log("Subscribe FPV feed " + ((response["result"].AsBool) ? "succeeded" : "failed"));
    }

    /// <summary>
    /// Unsubscribe to FPV camera
    /// </summary>
    public void UnsubscribeFPV()
    {
        string serviceName = "/dji_sdk/setup_camera_stream";
        string id = string.Format("{0} {1} unsubscribe FPV", client_id, serviceName);
        ros.CallService(HandleUnsubscribeFPVResponse, serviceName, id, "[0 0]");
    }
    /// <summary>
    /// Parse response to FPV camera unsubscription command
    /// </summary>
    /// <param name="response"></param>
    public void HandleUnsubscribeFPVResponse(JSONNode response)
    {
        response = response["values"];
        Debug.Log("Unsubscribe FPV feed " + ((response["result"].AsBool) ? "succeeded" : "failed"));
    }

    /// <summary>
    /// Subscribe to main camera
    /// </summary>
    public void SubscribeMainCamera()
    {
        string serviceName = "/dji_sdk/setup_camera_stream";
        string id = string.Format("{0} {1} subscribe MainCamera", client_id, serviceName);
        ros.CallService(HandleSubscribeMainCameraResponse, serviceName, id, "[1 1]");
    }
    /// <summary>
    /// Parse response to main camera subscription command
    /// </summary>
    /// <param name="response"></param>
    public void HandleSubscribeMainCameraResponse(JSONNode response)
    {
        response = response["values"];
        Debug.Log("Subscribe MainCamera feed " + ((response["result"].AsBool) ? "succeeded" : "failed"));
    }

    /// <summary>
    /// Unsubscribe to main camera
    /// </summary>
    public void UnsubscribeMainCamera()
    {
        string serviceName = "/dji_sdk/setup_camera_stream";
        string id = string.Format("{0} {1} unsubscribe MainCamera", client_id, serviceName);
        ros.CallService(HandleUnsubscribeMainCameraResponse, serviceName, id, "[1 0]");
    }
    /// <summary>
    /// Parse response to main camera unsubscription command
    /// </summary>
    /// <param name="response"></param>
    public void HandleUnsubscribeMainCameraResponse(JSONNode response)
    {
        response = response["values"];
        Debug.Log("Unsubscribe MainCamera feed " + ((response["result"].AsBool) ? "succeeded" : "failed"));
    }

}
