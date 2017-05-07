# Bebop2-C-SDK
Parrot Bebop2  SDK in C# for standalone PC

1. Introduction: 
    This is a C# SDK for Parrot Bebop2 pilotting. You can pilot the raw/yaw/pitch of the drone. Since this project is just started, it doesn't provide complete function as official SDK. I will keep updating the rest functions in the future.

2. Bebop Command Type: 

    (a) Each frame sent to drone includes:

            • Data type (1 byte)
            • Target buffer ID (1 byte)
            • Sequence number (1 byte)
            • Total size of the frame (4 bytes, Little endian) 
            • Actual data (N bytes)

    (b) Actual data can be considered as a command:

            • Project or Feature ID (1 byte)
            • Class ID in the project/feature (1 byte)
            • Command ID in the class (2 bytes)

3. The wifi connection:

    IP: 192.168.42.1
    The drone discovering command is sent to the drone in TCP, port: 44444
    The other commands are sent to drone in UDP, port: 54321

4. How to use: 

    Just need to use the namespace "drone_UDP".

    Step 1. new a BebopCommand object

    Step 2. use the pilot command in the object and enjoy it.

    * pilot command : 

        • takeoff() : make the drone takeoff.

        • landing() : make the drone landing.

        • move(int flag, int roll, int pitch, int yaw, int gaz) : pilot the drone through raw/yaw/pitch. All the parameters are described in the sameple file "Program.cs".

        • videoEnable() : start the video streaming of the drone. You can open the sdp file to watch the video from the drone.

        • cancleAllTask() : stop all the thread running in the background. (It will make the drone stop but loose the connection of the drone.)

5. NOTICE:

    Since this project is just started, it doesn't provide complete function as official SDK. I only implement the drone movement control command, which is roughly send the moving command to the drone but don't receive any return message from the drone.

    Because the streaming type is RTP/.H264 and this project is going to be used in UWP file, I'm still thinking that how to decode the RTP/H264 package on the UWP device. So basically if you want to watch the video from the drone, you need to start the video streaming and open the SDP file provided by the official.
    
