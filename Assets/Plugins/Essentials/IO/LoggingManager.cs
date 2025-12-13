using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Skeletom.Essentials.Lifecycle;
using UnityEngine;
using UnityEngine.Events;

namespace Skeletom.Essentials.IO
{
	public class LoggingManager : Singleton<LoggingManager>
	{
		private static string GLOBAL_LOG_DIRECTORY;
		public string LogDirectory { get { return GLOBAL_LOG_DIRECTORY; } }
		private const string LOG_FILE_PREFIX = "log";
		private const string LOG_FILE_NAME_FORMAT = "{0}_{1}.txt";
		private static string LOG_FILE_PATH_FULL;
		private const int MAX_LOG_FILES = 8;

		private static readonly ConcurrentQueue<LogRecord> QUEUE = new ConcurrentQueue<LogRecord>();
		private static readonly ReaderWriterLock LOCKER = new ReaderWriterLock();
		private static Thread WORKER;
		private static bool QUITTING = false;
		/// <summary>
		/// Gets the last statement logged. 
		/// </summary>
		public static LogRecord LastLogRecord { get; private set; }

		[Serializable]
		public struct LogRecord
		{
			public DateTime time;
			public LogType type;
			public string message;
			public string stack;

			public LogRecord(LogType type, string message, string stack)
			{
				this.message = message;
				this.stack = stack;
				this.type = type;
				this.time = DateTime.Now;
			}

			public override readonly string ToString()
			{
				return string.Format("[{0}] [{1}] - {2}\n",
					time.ToString("MM/dd/yyyy HH:mm:ss"),
					type,
					(type == LogType.Exception || type == LogType.Error) && stack != null && stack.Length > 0
					? string.Format("{0} - {1}", message, stack)
					: message);
			}
		}

		[Serializable]
		public class LogEvent : UnityEvent<LogRecord> { }
		/// <summary>
		/// Event fired when a log statement occurs. 
		/// TODO: hook this up
		/// </summary>
		public static LogEvent OnLog = new LogEvent();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
		public static void OnBoot()
		{
			// DontDestroyOnLoad(LoggingManager.Instance);
			GLOBAL_LOG_DIRECTORY = Path.Combine(Application.persistentDataPath, "Logs");
			if (!Directory.Exists(GLOBAL_LOG_DIRECTORY))
			{
				Directory.CreateDirectory(GLOBAL_LOG_DIRECTORY);
			}
			string timestamp = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
			LOG_FILE_PATH_FULL = Path.Combine(GLOBAL_LOG_DIRECTORY, string.Format(LOG_FILE_NAME_FORMAT, LOG_FILE_PREFIX, timestamp));
			Application.logMessageReceived += EnqueueLog;
			DeleteOldLogs();
			WORKER = new Thread(PrintToLog);
			WORKER.Start();
		}

		public override void Initialize()
		{
		}

		private void OnApplicationQuit()
		{
			QUITTING = true;
		}

		private static void EnqueueLog(string logString, string stackTrace, LogType type)
		{
			LogRecord record = new LogRecord(type, logString, stackTrace);
			QUEUE.Enqueue(record);
			OnLog.Invoke(record);
			LastLogRecord = record;
		}

		private static void PrintToLog()
		{
			do
			{
				string joined = "";
				do
				{
					if (QUEUE.TryDequeue(out LogRecord log))
					{
						joined += log.ToString();
					}
				} while (QUEUE.Count > 0);
				try
				{
					LOCKER.AcquireWriterLock(int.MaxValue);
					File.AppendAllText(LOG_FILE_PATH_FULL, joined, Encoding.UTF8);
				}
				finally
				{
					LOCKER.ReleaseWriterLock();
					joined = string.Empty;
				}
				Thread.Sleep(1);
			} while (QUEUE.Count > 0 || QUITTING == false);
		}

		/// <summary>
		/// Deletes old log files.
		/// </summary>
		/// <returns>False if failed, true otherwise.</returns>
		private static bool DeleteOldLogs()
		{
			try
			{
				List<string> oldLogFiles = new List<string>(Directory.GetFiles(GLOBAL_LOG_DIRECTORY));
				List<string> filteredLogFiles = oldLogFiles.FindAll(fileName => fileName.Contains(LOG_FILE_PREFIX));
				int logFilesToDelete = filteredLogFiles.Count - MAX_LOG_FILES;
				if (logFilesToDelete > 0)
				{
					filteredLogFiles.Sort((a, b) => File.GetCreationTime(a).CompareTo(File.GetCreationTime(b)));
					foreach (string oldLogName in filteredLogFiles)
					{
						try
						{
							Debug.Log("Deleting old log file: " + Path.GetFileName(oldLogName));
							File.Delete(oldLogName);
							logFilesToDelete--;
							if (logFilesToDelete <= 0)
							{
								break;
							}
						}
						catch (Exception e)
						{
							Debug.LogError(e);
						}
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogError(e);
			}

			return true;
		}
	}
}