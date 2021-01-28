using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

namespace GenerateTeacherKyokumen
{
	class Evaluates
	{
		public int besteval;
		public List<(string move, string eval)> moves;
		public Evaluates() { moves = new List<(string move, string eval)>(); }
	}

	class Program
	{
		static bool alive;
		static void Main(string[] args) {
			alive = true;
			Console.WriteLine("教師局面生成");
			while (alive) {
				Console.Write("command?(r/s/q) > ");
				switch (Console.ReadLine()) {
					case "r":
						resister();
						break;
					case "s":
						start();
						break;
					case "q":
					case "quit":
						alive = false;
						break;
				}
			}

		}

		static void resister() {
			const string settingpath = "./Player.usiplayer";
			const string playername = "Player";
			while (true) {
				Console.Write("input resisterd usi engine's path > ");
				string path = Console.ReadLine();
				try {
					Player player = new Player(path, playername);
					player.settingsave(settingpath);
					return;
				}
				catch (Exception e) {
					Console.WriteLine(e.Message);
				}
			}
		}

		static void start() {
			if (!File.Exists("./Player.usiplayer")) {
				Console.WriteLine("Player setting file does not exist.");
				return;
			}
			//出力フォルダ用意
			if (!Directory.Exists("./output")) {
				Directory.CreateDirectory("./output");
			}

			//設定
			const string shinchokupath = "./shinchoku.temp";
			string sfenpath;
			int time_ms = 1000;
			int startline = 0;
			int endline = 0;
			if (File.Exists(shinchokupath)) {
				using var shinchoku = new StreamReader(shinchokupath);
				sfenpath = shinchoku.ReadLine();
				time_ms = int.Parse(shinchoku.ReadLine());
				startline = int.Parse(shinchoku.ReadLine());
				endline = int.Parse(shinchoku.ReadLine());
			}
			else {
				Console.Write("sfen path? > ");
				sfenpath = Console.ReadLine();
				do {
					Console.Write("eval time? > ");
				} while (!int.TryParse(Console.ReadLine(), out time_ms));
				do {
					Console.Write("start line? > ");
				} while (!int.TryParse(Console.ReadLine(), out startline));
				bool result;
				do {
					Console.Write("num of kyokumen? > ");
					result = int.TryParse(Console.ReadLine(), out int num);
					endline = startline + num;
				} while (!result && endline > startline);
			}

			//将棋エンジン起動
			Player player = new Player("./Player.usiplayer");
			using var proc = new Process();
			player.Start(proc);

			//実行
			using var sfen = new StreamReader(sfenpath);
			const string joban_path = "./output/joban.txt";
			const string chuban_path = "./output/chuban.txt";
			const string shuban_path = "./output/syuban.txt";
			for (int t = 0; t < startline; t++) sfen.ReadLine();
			for (int t = startline; !sfen.EndOfStream && t < endline; t++) {
				using (var shinchoku = new StreamWriter(shinchokupath, false)) {
					shinchoku.WriteLine(sfenpath);
					shinchoku.WriteLine(time_ms.ToString());
					shinchoku.WriteLine(t.ToString());
					shinchoku.WriteLine(endline.ToString());
				}

				string sfenline = sfen.ReadLine();
				if (!sfenline.StartsWith("startpos moves ") && !sfenline.StartsWith("sfen ")) break;

				var eval = genTeacher(proc, sfenline, time_ms);
				var output_path = (Math.Abs(eval.besteval) < 500) ? joban_path : ((Math.Abs(eval.besteval) < 2500) ? chuban_path : shuban_path);
				using var sw = new StreamWriter(output_path, true);
				sw.WriteLine(sfenline);
				foreach (var child in eval.moves) {
					sw.Write(child.move + " " + child.eval + ", ");
				}
				sw.WriteLine();
				Console.Write(".");
			}
			proc.StandardInput.WriteLine("quit");
			proc.Kill();
			proc.WaitForExit();
			File.Delete(shinchokupath);
			using var log = new StreamWriter("./log.txt", true);
			log.WriteLine(startline + "->" + (endline-1) + " " + sfenpath);
			Console.WriteLine("\nfinished.");
		}

		static string getInfoValue(string[] tokens,string key) {
			for(int i = 1; i < tokens.Length; i++) {
				if (tokens[i] == key) {
					return tokens[i + 1];
				}
			}
			return "";
		}

		static Evaluates genTeacher(Process proc, string sfen, int time) {
			Evaluates result = new Evaluates();
			proc.StandardInput.WriteLine("usinewgame");
			proc.StandardInput.WriteLine("position " + sfen);
			proc.StandardInput.WriteLine("go btime 0 wtime 0 byoyomi " + time.ToString());
			int pvnum = 0;
			while (true) {
				string usiline = proc.StandardOutput.ReadLine();
				var token = usiline.Split(" ");
				if (token == null || token.Length == 0) continue;
				if (token[0] == "info") {
					string eval = getInfoValue(token, "cp");
					string multipv = getInfoValue(token, "multipv");
					if(int.TryParse(multipv,out int mpnum)) {
						if (mpnum <= pvnum) {
							result.moves.Clear();
							pvnum = 0;
							result.besteval = int.Parse(eval);
						}
						result.moves.Add((getInfoValue(token, "pv"), eval));
						pvnum++;
					}
				}
				else if (token[0] == "bestmove") {
					break;
				}
			}
			proc.StandardInput.WriteLine("gameover win");
			return result;
		}
	}
}
