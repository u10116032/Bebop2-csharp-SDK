using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading.Tasks;
using System.Threading;



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

		private UdpClient arstreamClient;
		private IPEndPoint remoteIpEndPoint;

		private UdpClient d2c_client;

		private byte[] receivedData;
		private static object _thisLock = new object();


		//int frameCount = 0;


		public int Discover() {
            Console.WriteLine("Discovering...");

			d2c_client = new UdpClient(CommandSet.IP, 54321);


			//make handshake with TCP_client, and the port is set to be 4444
			TcpClient tcpClient = new TcpClient(CommandSet.IP, CommandSet.DISCOVERY_PORT);
            NetworkStream stream = new NetworkStream(tcpClient.Client);

            //initialize reader and writer
            StreamWriter streamWriter = new StreamWriter(stream);
            StreamReader streamReader = new StreamReader(stream);

			//when the drone receive the message bellow, it will return the confirmation
			string handshake_Message = "{\"controller_type\":\"computer\", \"controller_name\":\"halley\", \"d2c_port\":\"43210\", \"arstream2_client_stream_port\":\"55004\", \"arstream2_client_control_port\":\"55005\"}";
            streamWriter.WriteLine(handshake_Message);
            streamWriter.Flush();
            
        


            string receive_Message = streamReader.ReadLine();
            if (receive_Message == null)
            {
                Console.WriteLine("Discover failed");
                return -1;
            }
            else {
                Console.WriteLine("The message from the drone shows: " + receive_Message);

				//initialize
				cmd = default(Command);
				pcmd = default(PCMD);

				//All State setting
				generateAllStates();
                generateAllSettings();

				//enable video streaming
				videoEnable();

				//init ARStream
				//initARStream();

				//init CancellationToken
				cancelToken = cts.Token;


				pcmdThreadActive();
				//arStreamThreadActive();
                return 1;
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


            d2c_client.Send(buf, buf.Length);


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

		public void initARStream() 
		{
			arstreamClient = new UdpClient(55004);
			remoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
		}

		public void getImageData() 
		{
			//Console.WriteLine("Receiving...");

			receivedData = arstreamClient.Receive(ref remoteIpEndPoint);
			Console.WriteLine("Receive Data: " + BitConverter.ToString(receivedData));
			//frameCount++;
			//arstreamClient.BeginReceive(new AsyncCallback(recvData), null);
		}


		public void arStreamThreadActive() 
		{
			Console.WriteLine("The ARStream thread is starting");

			Task.Factory.StartNew(() =>
			{
				while (true)
				{
					//Thread.Sleep(1000);
					getImageData();
				}
			}, cancelToken);
		}


    }
}
