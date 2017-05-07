using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace drone_UDP
{
    class Program
    {
        static void Main(string[] args)
        {
            
            //This is a sample about using the pilotting command.
            
            BebopCommand bebop = new BebopCommand();
            if (bebop.Discover() == -1) {
                Console.ReadLine();
                return;
            }
            else {
                while (true) {
					
                    string input = Console.ReadLine();
					if (input == "t")  //takeoff
						bebop.takeoff();
					else if (input == "l")  //landing
						bebop.landing();

                    //moving command: -100% ~ 100%
                    
					else if (input == "a")  //left
						bebop.move(1, -10, 0, 0, 0);
					else if (input == "d")  //right
						bebop.move(1, 10, 0, 0, 0);
					else if (input == "w")  //forward
						bebop.move(1, 0, 10, 0, 0);
					else if (input == "s")  //backward
						bebop.move(1, 0, -10, 0, 0);
					else if (input == "h") //turn left
						bebop.move(0, 0, 0, -10, 0);
					else if (input == "k")  //turn right
						bebop.move(0, 0, 0, 10, 0);
					else if (input == "u")  //up
						bebop.move(0, 0, 0, 0, 10);
					else if (input == "j")  //down
						bebop.move(0, 0, 0, 0, -10);
					else if (input == "p")  //pause
						bebop.move(0, 0, 0, 0, 0);

					else if (input == "v")
						bebop.videoEnable(); //enable RTP/.H264 videostreaming
					else if (input == "q")  //quit
					{
						bebop.cancleAllTask();
						return;
					}

                }
            }
        }
    }
}
