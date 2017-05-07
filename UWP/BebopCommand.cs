using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Foundation;



using BebopCommandSet;


namespace drone_UDP
{

    class BebopCommand
    {
        private int[] seq = new int[256];
        private Command cmd;
		private PCMD pcmd;

        private CancellationTokenSource cts = new CancellationTokenSource();
        private CancellationToken cancelToken;

        private Mutex pcmdMtx = new Mutex();

		private static object _thisLock = new object();
        
        private DatagramSocket udpSocket;
        private StreamSocket tcpSocket;


		//int frameCount = 0;


        async void Discover() {
            Debug.Log("Discovering...");
            //make handshake with TCP_client, and the port is set to be 4444
            tcpSocket = new StreamSocket();
            HostName networkHost = new HostName(CommandSet.IP.Trim());
            
            try
            {
                await tcpSocket.ConnectAsync(networkHost, CommandSet.DISCOVERY_PORT.ToString());
                Debug.Log("tcpSocket Connected!");
            } catch (Exception e) {
                Debug.Log(e.ToString());
                Debug.Log(SocketError.GetStatus(e.HResult).ToString());
                return;
            }
            
            
            //Write data to the echo server.
            Stream streamOut = tcpSocket.OutputStream.AsStreamForWrite();
            StreamWriter writer = new StreamWriter(streamOut);
            
            //when the drone receive the message bellow, it will return the confirmation
            string handshake_Message = "{\"controller_type\":\"computer\", \"controller_name\":\"halley\", \"d2c_port\":\"43210\", \"arstream2_client_stream_port\":\"55004\",\"arstream2_client_control_port\":\"55005\"}";
            try
            {
                await writer.WriteLineAsync(handshake_Message);
                await writer.FlushAsync();
                Debug.Log("tcpSocket writer successful!");
            } catch (Exception e) {
                Debug.Log(e.ToString());
                Debug.Log(SocketError.GetStatus(e.HResult).ToString());
                return;
            }
            
            //Read data from the echo server.

            Stream streamIn = tcpSocket.InputStream.AsStreamForRead();
            StreamReader reader = new StreamReader(streamIn);
            string receive_Message = await reader.ReadLineAsync();
            
            if (receive_Message == null)
            {
                Debug.Log("Discover failed");
                //return -1;
                isConnected = false;
            } else {
                Debug.Log("The message from the drone shows: " + receive_Message);
                
                //initialize
                initPCMD();
                initCMD();
                
                //All State setting
                generateAllStates();
                generateAllSettings();
                isConnected = true;
                Debug.Log("isConnected = true");
                pcmdThreadActive();
                //return 1;
            }
        }

        private async void SendMessage(byte[] buf)
        {
            udpSocket = new DatagramSocket();
            try
            {
                HostName networkHost = new HostName(CommandSet.IP.Trim());
                IOutputStream outputStream;
                outputStream = await udpSocket.GetOutputStreamAsync(networkHost, "54321");
                DataWriter writer = new DataWriter(outputStream);
                writer.WriteBytes(buf);
                await writer.StoreAsync();
            } catch (Exception e) {
                Debug.Log(e.ToString());
                Debug.Log(SocketError.GetStatus(e.HResult).ToString());
                return;
            }
        }


        public void sendCommandAdpator(ref Command cmd, int type = CommandSet.ARNETWORKAL_FRAME_TYPE_DATA, int id = CommandSet.BD_NET_CD_NONACK_ID) {
                            int bufSize = cmd.size + 7;
                            byte[] buf = new byte[bufSize];

                            seq[id]++;
                            if (seq[id] > 255) seq[id] = 0;

                            buf[0] = (byte)type;
                            buf[1] = (byte)id;
                            buf[2] = (byte)seq[id];
                            buf[3] = (byte)(bufSize & 0xff);
                            buf[4] = (byte)((bufSize & 0xff00) >> 8);
                            buf[5] = (byte)((bufSize & 0xff0000) >> 16);
                            buf[6] = (byte)((bufSize & 0xff000000) >> 24);

                            cmd.cmd.CopyTo(buf, 7);


                            SendMessage(buf);


                        }

        public void takeoff() {
            Console.WriteLine("try to takeoff ing...");
            cmd = default(Command);
            cmd.size = 4;
            cmd.cmd = new byte[4];

            cmd.cmd[0] = CommandSet.ARCOMMANDS_ID_PROJECT_ARDRONE3;
            cmd.cmd[1] = CommandSet.ARCOMMANDS_ID_ARDRONE3_CLASS_PILOTING;
            cmd.cmd[2] = CommandSet.ARCOMMANDS_ID_ARDRONE3_PILOTING_CMD_TAKEOFF;
            cmd.cmd[3] = 0;

            sendCommandAdpator(ref cmd, CommandSet.ARNETWORKAL_FRAME_TYPE_DATA_WITH_ACK, CommandSet.BD_NET_CD_ACK_ID);
        }

        public void landing() {
            Console.WriteLine("try to landing...");
            cmd = default(Command);
            cmd.size = 4;
            cmd.cmd = new byte[4];

            cmd.cmd[0] = CommandSet.ARCOMMANDS_ID_PROJECT_ARDRONE3;
            cmd.cmd[1] = CommandSet.ARCOMMANDS_ID_ARDRONE3_CLASS_PILOTING;
            cmd.cmd[2] = CommandSet.ARCOMMANDS_ID_ARDRONE3_PILOTING_CMD_LANDING;
            cmd.cmd[3] = 0;

            sendCommandAdpator(ref cmd, CommandSet.ARNETWORKAL_FRAME_TYPE_DATA_WITH_ACK, CommandSet.BD_NET_CD_ACK_ID);
        }

		public void move(int flag, int roll, int pitch, int yaw, int gaz) {
			pcmd.flag = flag;
			pcmd.roll = roll;
			pcmd.pitch = pitch;
			pcmd.yaw = yaw;
			pcmd.gaz = gaz;

			/*var task = Task.Factory.StartNew(() =>
			{
				Console.WriteLine("move thread start");
				generatePCMD(flag, roll, pitch, yaw, gaz);
			}, cancelToken);
			task.Wait();
			Console.WriteLine("move thread end");
			task.Dispose();*/
		}

        public void generatePCMD() {
			lock(_thisLock) { 
				cmd = default(Command);
	            cmd.size = 13;
	            cmd.cmd = new byte[13];

	            cmd.cmd[0] = CommandSet.ARCOMMANDS_ID_PROJECT_ARDRONE3;
	            cmd.cmd[1] = CommandSet.ARCOMMANDS_ID_ARDRONE3_CLASS_PILOTING;
	            cmd.cmd[2] = CommandSet.ARCOMMANDS_ID_ARDRONE3_PILOTING_CMD_PCMD;
	            cmd.cmd[3] = 0;

	            //pcmdMtx.WaitOne();
	            cmd.cmd[4] = (byte)pcmd.flag;  // flag
	            cmd.cmd[5] = (pcmd.roll >= 0 ) ?　(byte)pcmd.roll: (byte)(256 + pcmd.roll);  // roll: fly left or right [-100 ~ 100]
	            cmd.cmd[6] = (pcmd.pitch >= 0) ? (byte)pcmd.pitch : (byte)(256 + pcmd.pitch);  // pitch: backward or forward [-100 ~ 100]
	            cmd.cmd[7] = (pcmd.yaw >= 0) ? (byte)pcmd.yaw : (byte)(256 + pcmd.yaw);  // yaw: rotate left or right [-100 ~ 100]
	            cmd.cmd[8] = (pcmd.gaz >= 0) ? (byte)pcmd.gaz : (byte)(256 + pcmd.gaz);  // gaze: down or up [-100 ~ 100]


				// for Debug Mode
				cmd.cmd[9] = 0;
	            cmd.cmd[10] = 0;
	            cmd.cmd[11] = 0;
	            cmd.cmd[12] = 0;

				sendCommandAdpator(ref cmd);
			}

		}

        public void pcmdThreadActive() {

            Console.WriteLine("The PCMD thread is starting");

            Task.Factory.StartNew(() => {
                while (true) {
                    generatePCMD();
                    Thread.Sleep(50);  //sleep 50ms each time.
                }  
            }, cancelToken);
            
        }

        public void cancleAllTask() {
            cts.Cancel();
			//Console.WriteLine(frameCount);
        }

        public void generateAllStates() {
            Console.WriteLine("Generate All State");
            cmd = default(Command);
            cmd.size = 4;
            cmd.cmd = new byte[4];

            cmd.cmd[0] = CommandSet.ARCOMMANDS_ID_PROJECT_COMMON;
            cmd.cmd[1] = CommandSet.ARCOMMANDS_ID_COMMON_CLASS_COMMON;
            cmd.cmd[2] = ( CommandSet.ARCOMMANDS_ID_COMMON_COMMON_CMD_ALLSTATES & 0xff);
            cmd.cmd[3] = ( CommandSet.ARCOMMANDS_ID_COMMON_COMMON_CMD_ALLSTATES & 0xff00 >> 8);

            sendCommandAdpator(ref cmd, CommandSet.ARNETWORKAL_FRAME_TYPE_DATA_WITH_ACK, CommandSet.BD_NET_CD_ACK_ID);
        }

        public void generateAllSettings()
        {
            Console.WriteLine("Generate All Settings");
            cmd = default(Command);
            cmd.size = 4;
            cmd.cmd = new byte[4];

            cmd.cmd[0] = CommandSet.ARCOMMANDS_ID_PROJECT_COMMON;
            cmd.cmd[1] = CommandSet.ARCOMMANDS_ID_COMMON_CLASS_SETTINGS;
            cmd.cmd[2] = (0 & 0xff); // ARCOMMANDS_ID_COMMON_CLASS_SETTINGS_CMD_ALLSETTINGS = 0
            cmd.cmd[3] = (0 & 0xff00 >> 8);

            sendCommandAdpator(ref cmd, CommandSet.ARNETWORKAL_FRAME_TYPE_DATA_WITH_ACK, CommandSet.BD_NET_CD_ACK_ID);
        }

        public void videoEnable() 
		{
			Console.WriteLine("Send Video Enable Command");
			cmd = default(Command);
			cmd.size = 5;
			cmd.cmd = new byte[5];

			cmd.cmd[0] = CommandSet.ARCOMMANDS_ID_PROJECT_ARDRONE3;
			cmd.cmd[1] = CommandSet.ARCOMMANDS_ID_ARDRONE3_CLASS_MEDIASTREAMING;
			cmd.cmd[2] = (0 & 0xff); // ARCOMMANDS_ID_COMMON_CLASS_SETTINGS_CMD_VIDEOENABLE = 0
			cmd.cmd[3] = (0 & 0xff00 >> 8);
			cmd.cmd[4] = 1; //arg: Enable

			sendCommandAdpator(ref cmd, CommandSet.ARNETWORKAL_FRAME_TYPE_DATA_WITH_ACK, CommandSet.BD_NET_CD_ACK_ID);
		}





    }
}
