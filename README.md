# Immersive Semi-Autonomous Aerial Command System
## Virtual Reality Interface for Multi-UAV Teleoperation, Version 2

[![Overview Video](http://img.youtube.com/vi/msqf3BXUMIg/0.jpg)](http://www.youtube.com/watch?v=msqf3BXUMIg "ISAACS Overview - PCARI Presentation")

<br>

ISAACS is an undergraduate-led research group within the [Center for Augmented Cognition](https://ptolemy.berkeley.edu/projects/augcog/) of the [VHL Vive Center for Enhanced Reality](https://vivecenter.berkeley.edu/research1/isaacs/) at the University of California, Berkeley. Our research is in human-UAV interaction, with a focus on teleoperation, telesensing, and multi-agent interaction. We are also part of the student group [Extended Reality @ Berkeley](https://xr.berkeley.edu/), and collaborate with the Lawrence Berkeley National Laboratory to perform 3D reconstruction of the environment via state-of-the-art methods in radiation detection. Our vision is to create a scalable open source platform for Beyond Line of Sight Flight compatible with any UAV or sensor suite.

<br>


## Table of Contents
1. [ Hardware Setup ](#hardware)
2. [ Software Setup ](#software)
3. [ Installation and Deployment ](#installation)
4. [ Meet the Team ](#team)
5. [ Acknowledgments ](#acknowledgments)
6. [ Licensing ](#licensing)
<br>
<br>

<a name="hardware"></a>
## 1. Hardware Dependencies and Setup
You will need the following to run the system in simulation:
- DJI Matrice 210 Quadrotor
- DJI Matrice 210 RTK
- 2x Matrice 210 Quadrotor Batteries
- 1x Matrice 210 RTK Battery
- DJI Matrice Manifold 2 Onboard Computer
- 1x USB 3.0 to TTL Cable
- 1x USB 3.0 to USB 3.0 Cable
- Oculus Rift Virtual Reality Headset
- VR-Ready Computer (we suggest a GeForce GTX 970 Graphics Card or better)
- An Ethernet Cable

Additionally, to fly the UAV in real space, you will need:
- DJI Matrice RTK GPS Station
- 1x USB 3.0 Wi-Fi Card
- 1x Matrice 210 Quadrotor Battery
- A Wi-Fi Source

<br> You will have to connect the Manifold USB 3.0 port to the Matrice 210 UART port, using the USB 3.0 to TTL Cable. Refer to [this page](https://developer.dji.com/onboard-sdk/documentation/development-workflow/hardware-setup.html) for more information. Unlike what is described in the DJI documentation, we found out than on our Matrice 210, the TX and RX pins where inverted, meaning that TX is the white pin, RX is the green pin, and GND is the black pin. You also want to make sure that the gray Power slider is slided all the way to the left. <br>

You will moreover need to plug-in, into the Manifold, a USB 3.0 Wi-Fi card (if you plan on flying the UAV in real space), or an Ethernet cable (if you only plan on running the system in simulation). Also, to facilitate the next steps, you may want to connect the manifold to a keyboard and a screen, using an HDMI cable. If not, you can always SSH into it. <br>

Once you have done the above, place two batteries in the UAV and plug-in the Manifold power cord. Then, double-press and hold the orthogonal white button in front of the Matrice 210 UAV, and finally press and hold the PWR button of the Manifold. If everything went well, the UAV will play a sound, and the Manifold computer will boot.
<br>
<br>


<a name="software"></a>
## 2. Software Dependencies and Setup
The system uses two computers, one attached to the UAV, which we call **Manifold**, and one running the VR interface, which we call **VR-Ready Computer**. You may also use a third computer to run a flight simulation using the [DJI Assistant 2 for Matrice](https://www.dji.com/jp/downloads/softwares/assistant-dji-2-for-matrice), but this can be done on the VR-Ready Computer simultaneously as the frontend application is running. The Manifold backend depends on [ROS Kinetic](https://wiki.ros.org/kinetic), which requires Ubuntu 16.04 (Xenial), or another Debian-based GNU/Linux distribution. You will furthermore need the [ROS DJI SDK](https://wiki.ros.org/dji_sdk), and a [Rosbridge Server](https://wiki.ros.org/rosbridge_suite). The frontend interface depends on **Unity 2017.4.32f1**, and can be run on any platform, but has only been tested on Windows 10. <br>
Although the manifold comes with most things you need installed by default, you will have to setup a ROS Workspace and the Rosbridge Server. Refer to [this page](https://developer.dji.com/onboard-sdk/documentation/development-workflow/sample-setup.html) for more information on how to setup a ROS Workspace. <br>

### Common Problems when Setting up a Workspace
**\`catkin make\` does not compile** <br>
You might need to clone the [nmea_msgs](https://github.com/ros-drivers/nmea_msgs) package into the `src` folder, and then try again.
<br>

**I'm editing the sdk.launch file with \`rosed\`, but I cannot find the correct serial port** <br>
This will in most cases be `/dev/ttyUSB0`. If this is incorrect, then an error will pop up. To find the correct serial port:

- `$` `grep -iP PRODUCT= /sys/bus/usb-serial/devices/ttyUSB0/../uevent`
CAUTION: there is a space between PRODUCT= and /sys'. This is not a typo.
- `$` `lsusb | grep <ID>`
Replace \<ID\> with the ID found from the previous step.
<br>

**I don't know what to set the Baudrate to** <br>
The Baudrate should be set to 921600. If you are using the DJI Assistant 2 for Matrice to simulate a flight, then you also need to set the same Baudrate inside the DJI Assistant 2 for Matrice app, which can be found under the SDK tab.
<br>

**Connecting to the simulator and launching the SDK fails for an unknown reason** <br>
This can be due to many reasons, but generally it means tht you have to set a udev exception, and/or disable advanced sensing and connect the Manifold with the UAV with an additional USB 3.0 to USB 3.0 cable. CAUTION: disabling advanced sensing disables the Matrice 210's built-in object avoidance mechanism.

- `$` `echo 'SUBSYSTEM=="usb", ATTRS{idVendor}=="2ca3", MODE="0666"' | sudo tee /etc/udev/rules.d/m210.rules`
- Change `enable_adanced_sensing` to `false` in the file `DJI/catkin_ws/Onboard-SDK-ROS/dji_sdk/src/modules/dji_sdk_node.cpp`

### Installing the Rosbridge Server on the Manifold
`$` `sudo apt-get install ros-kinetic-rosbridge-server`
<br>

### Installing Unity on the VR-Ready Computer
Unity versions and installation instructions can be found on [this page](https://unity3d.com/get-unity/download/archive).
<br>
<br>

<a name="installation"></a>
## 3. Installation and Deployment
Make sure that you red and went through the Hardware Dependencies and Software Dependencies section, before proceeding with the system installation. This is critically important; the system will not work otherwise. <br>

### Installation (Simulation only)
1. Clone the project on the VR-Ready Computer with the following command:
<br> `$` `git clone https://github.com/immersive-command-system/ImmersiveDroneInterface_2.git`
1. Initialize Submodules: `git submodule update --init --recursive`
2. Place the RTK Battery inside the RTK Controller, and turn it on.
3. Disable RTK Signal (you may need to connect the controller to a phone or tablet with the 'DJI Go 4' app for this step)
4. Modify the Manifold's .bashrc to source ROS environment variables:
<br> `$` `echo 'cd $HOME/DJI/catkin_ws && source devel/setup.bash' >> $HOME/.bashrc`
5. In a new terminal, start the DJI SDK:
<br> `$` `roslaunch dji_sdk sdk.launch`
6. Test if the UAV can receive Manifold instructions by running the following command (this should spin the rotors, without actually flying the drone):
<br> `$` `rosservice call /dji_sdk/sdk_control_authority 1`
<br> `$` `rosservice call /dji_sdk/drone_arm_control 1`
7. If the rotor spin, great, we are almost there! Stop the rotors with the following command:
<br> `$` `rosservice call /dji_sdk/drone_arm_control 0`
8. Check that the Manifold is correctly connected to the Ethernet cable. Connect the other end of the Ethernet cable to the VR-Ready computer.
9. Run the Rosbridge Server. This will launch a WebSocket in port 9090. If you want to use a different port, see [this page](https://wiki.ros.org/rosbridge_suite/Tutorials/RunningRosbridge).
<br> `$` `roslaunch rosbridge_server rosbridge_websocket.launch`
10. Connect the Oculus headset with the VR-Ready laptop. If you have not done so already, follow through the [Oculus Rift setup](https://www.oculus.com/setup/).
11. Connect the Manifold to a computer with the DJI Assistant 2 for Matrice using a USB 3.0 to USB 3.0 cable, and launch the Flight Simulator.
12. Launch our application via Unity. Find the script named `ROSDroneConnection.cs` _**NOTE: deprecated, use the Unity GUI instead.**_ and replace the IP address of the server with the actual IP address of the Manifold. To find the IP address of the Manifold, use the following command:
<br> `$` `hostname -I`
13. Save and close the script, and launch our application by clicking on the play (small triangle) button inside Unity. If all went well, you should see printed information that a client connected to the Rosbridge Server, inside the terminal from which the Rosbridge server was launched.
14. Congratulations, you are ready to fly your UAV in VR!


### Installation (with UAV flight)
Follow the steps 1-11 as above, skipping step 12. Then, setup the RTK GPS Station.
Finally, continue with steps 13-15.
<br>

### Deployment
Each time that you want to run our system, you will have to first disable the RTK signal, and then run the DJI SDK and Rosbridge Server. The routine is rather simple:
1. Power-on the UAV, Manifold and VR-Ready Computer
2. (Optionally) connect the UAV to the DJI Assistant 2 for Matrice, and launch the Flight Simulator
3. Turn of the RTK signal through the 'DJI Go 4' app
4. Launch the SDK
<br> `$` `roslaunch dji_sdk sdk.launch`
5. Launch the Rosbridge Server
<br> `$` `roslaunch rosbridge_server rosbridge_websocket.launch`
6. Open our system in Unity and click the play button

Moreover, each time your internet connection changes, you will have to change the IP address that the Unity client subscribes to.
<br>
<br>

<a name="team"></a>
## 6. Meet the Team

### Current
[Eric Wang](https://www.linkedin.com/in/erwang01)<br>
Jasmine Bae<br>
Varun Saran<br>
[Nitzan Orr](https://orrblue.github.io/hello/)<br>
[Shreyas Krishnaswamy](https://www.linkedin.com/in/shreyas-krishnaswamy)<br>


### Alumni
[Jesse Patterson](http://www.jessepaterson.com/) <br>
[Jessica Lee](https://www.linkedin.com/in/jess-l/) <br>
[Peru Dayani](https://perudayani.github.io/perudayani/)<br>
[Apollo](https://apollo.vision)<br>
Ji Han <br>
Xin Chen <br>
Paxtan Laker <br>
Rishi Upadhyay <br>
Brian Wu <br>
Eric Zhang <br>
[Newman Hu](https://www.linkedin.com/in/newmanhu/)<br>
<br>

<a name="acknowledgments"></a>
## 7. Acknowledgments
We would like to thank [Dr. Allen Yang](https://people.eecs.berkeley.edu/~yang/) and [Dr. Kai Vetter](https://vcresearch.berkeley.edu/faculty/kai-vetter) for their mentorship and supervision. We would also like to thank our graduate advisors, [David McPherson](https://people.eecs.berkeley.edu/~david.mcpherson/) and [Joe Menke](https://people.eecs.berkeley.edu/~joemenke/) for their continuous support.
<br>
<br>

<a name="licensing"></a>
## 8. Licensing
This repository is distributed under the Apache 2.0 license. All media files are distributed under the Creative Commons Attribution-ShareAlike 4.0 International license. <br>
<br>
In case of doubt on whether you can use an asset, or on how to correctly attribute its authors, please e-mail us at: isaacs@xr.berkeley.edu.
