﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FreeNet;
using GameServer;
using MySql.Data.MySqlClient;

namespace CSampleServer
{
	class Program
	{
        static MySqlConnection conn;
		static List<CGameUser> userlist;

        static void Main(string[] args)
		{
			CPacketBufferManager.initialize(2000);
			userlist = new List<CGameUser>();

			CNetworkService service = new CNetworkService();
			// 콜백 매소드 설정.
			service.session_created_callback += on_session_created;
			// 초기화.
			service.initialize();

			var host = Dns.GetHostEntry(Dns.GetHostName());
			string local_IP = "";

			foreach(var ip in host.AddressList)
            {
				if(ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
					local_IP = ip.ToString();
					break;
				}
            }

            //Access mysql database. And check if the connection is successful.
            string connStr = "server=localhost;user=root;database=ChatLog;port=3306;password=vhzptapahflA123";
            conn = new MySqlConnection(connStr);
			
            try
            {
                Console.WriteLine("Connecting to MySQL...");
                conn.Open();
				
                //Check for connection
                if (conn.State == System.Data.ConnectionState.Open)
                {
                    Console.WriteLine("Connection is successful.");
                }
                else
                {
                    Console.WriteLine("Connection is failed.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            //service.listen("127.0.0.1", 7979, 100); // IP를 직접 입력하는 방식
            Console.WriteLine(string.Format("Get Local IP -> {0}", local_IP)); // 현재 컴퓨터의 IP 주소를 가져오는 방식
			service.listen(local_IP, 7979, 100); // 포트는 7979로 고정

			Console.WriteLine("Started!");
			while (true)
			{
				//Console.Write(".");
				System.Threading.Thread.Sleep(1000);
			}

			Console.ReadKey();
		}

		/// <summary>
		/// 클라이언트가 접속 완료 하였을 때 호출됩니다.
		/// n개의 워커 스레드에서 호출될 수 있으므로 공유 자원 접근시 동기화 처리를 해줘야 합니다.
		/// </summary>
		/// <returns></returns>
		static void on_session_created(CUserToken token)
		{
			CGameUser user = new CGameUser(token);
			user.callback_get_tokenlist += GetTokenList;

			lock (userlist)
			{
				userlist.Add(user);
			}
		}

		/// <summary>
		/// 클라이언트가 접속 해제를 하였을 때 호출됩니다.
		/// </summary>
		/// <param name="user"></param>
		public static void remove_user(CGameUser user)
		{
			lock (userlist)
			{
				userlist.Remove(user);
			}
		}

		/// <summary>
		/// 서버에 접속한 클라이언트 토큰 리스트를 반환한다.
		/// </summary>
		/// <returns>클라이언트 토큰 리스트</returns>
		public static List<CGameUser> GetTokenList()
        {
			return userlist;
        }

		/// <summary>
		/// 지금까지 기록된 채팅 로그를 데이터베이스에 저장한다.
		/// </summary>
		public static void MySqlSaveData(string message)
		{
            //The data in the chatlog_list list are stored in the chatlog_table table in the order in which they are listed. When saving a table, data is stored in the chatLog_Message column.
            string sql = "INSERT INTO chatlog_table(chatLog_Message,chatLog_Date) VALUES(@chatLog_Message,@chatLog_Date)";
            MySqlCommand cmd = new MySqlCommand(sql, conn);

            //Take the current date and time and save it in the form of string.
            string date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            cmd.Parameters.AddWithValue("@chatLog_Message", message);
            cmd.Parameters.AddWithValue("@chatLog_Date", date);
            cmd.ExecuteNonQuery();

            Console.WriteLine("Data is saved.");
        }

        /// <summary>
        /// 데이터베이스에 저장된 채팅 로그를 가져온다.
        /// </summary>
        /// <returns>저장된 채팅 로그</returns>
        public static List<Dictionary<string, string>> MySqlGetChatLog()
		{
            //In the Mysql ChatLog data, if there is data in the chatlog_table table, the chatLog_Message column in the chatLog_dic dictionary is the value of the cl_message key, and the chatLog_Date column is added as the value of the cl_date key and returned to the list.
            List<Dictionary<string, string>> chatLogList = new List<Dictionary<string, string>>();
            string sql = "SELECT chatLog_Message, chatLog_Date FROM chatlog_table";
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();

            while (rdr.Read())
            {
                Dictionary<string, string> chatLog_dic = new Dictionary<string, string>
                {
                    { "cl_message", rdr[0].ToString() },
                    { "cl_date", rdr[1].ToString() }
                };
                chatLogList.Add(chatLog_dic);
            }

            rdr.Close();

            return chatLogList;
        }

        public static bool IsSameMemberInDB(string member)
		{
            //Check the chatlog_member table data for data that matches the member column.
            string sql = "SELECT * FROM chatlog_member WHERE member = @member";
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@member", member);
            MySqlDataReader rdr = cmd.ExecuteReader();

            //If there is a matching member, return true.
            if (rdr.Read())
            {
                rdr.Close();
                return true;
            }

            rdr.Close();
            return false;
        }

        public static string GetFirstDateMember(string member)
        {
            //If the data in the chatlog_member table data matches the member column, it returns the data in the fastdate column in the form of a string, or in the form of an empty string.
            string sql = "SELECT firstdate FROM chatlog_member WHERE member = @member";
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@member", member);
            MySqlDataReader rdr = cmd.ExecuteReader();

            if (rdr.Read())
            {
                string fastdate = rdr[0].ToString();
                rdr.Close();
                return fastdate;
            }

            rdr.Close();
            return "";
        }

        public static void SettingMemberData(string new_member)
        {
            //If the new_member parameter is not in the ChatLog data chatlog_member table member column, add the current date time to the new_member parameter firstdate column in the member column.
            if (!IsSameMemberInDB(new_member))
            {
                string sql = "INSERT INTO chatlog_member(member,firstdate) VALUES(@member,@firstdate)";
                MySqlCommand cmd = new MySqlCommand(sql, conn);

                string date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                cmd.Parameters.AddWithValue("@member", new_member);
                cmd.Parameters.AddWithValue("@firstdate", date);
                cmd.ExecuteNonQuery();
            }
        }

        public static bool AccountCheckLogin(string id, string pw)
        {
            //Check if the parameter id, pw values match the id, pw values in the account_data_table table in the chat_database database. Returns true if there is a match, false if not.
            string sql = "SELECT * FROM account_data_table WHERE id = @id AND pw = @pw";
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@pw", pw);
            MySqlDataReader rdr = cmd.ExecuteReader();

            if (rdr.Read())
            {
                rdr.Close();
                return true;
            }

            rdr.Close();
            return false;
        }

        public static bool ChreateAccountCheck(string id)
        {
            //Check if the parameter id value matches the id value in the account_data_table table in the chat_database database. Returns true if there is a match, false if not.
            string sql = "SELECT * FROM account_data_table WHERE id = @id";
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            MySqlDataReader rdr = cmd.ExecuteReader();

            if (rdr.Read())
            {
                rdr.Close();
                return true;
            }

            rdr.Close();
            return false;
        }

        public static bool CreateAccount(string id, string pw)
        {
            //If the id, pw values in the parameter do not match the id, pw values in the account_data_table table in the chat_database database, add the id, pw values to the account_data_table table.
            if (!ChreateAccountCheck(id))
            {
                string sql = "INSERT INTO account_data_table(id,pw, create_date) VALUES(@id,@pw,@date)";
                MySqlCommand cmd = new MySqlCommand(sql, conn);

                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@pw", pw);
                cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.ExecuteNonQuery();

                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
