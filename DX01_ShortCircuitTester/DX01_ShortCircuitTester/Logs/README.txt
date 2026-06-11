測試紀錄輸出資料夾。

執行時 CSV 會寫到「執行檔旁」的 Logs 資料夾：
  bin\Debug\net48\Logs\DX01_yyyyMMdd.csv

每天一個檔案，每個測試步驟一列，欄位：
  時間, 序號, 整體判定, 步驟, 步驟名稱, Relay, 模式, 量測值, 單位, 下限, 上限, 步驟判定

若要改輸出位置，設定 CsvLogger.LogDirectory 即可。
