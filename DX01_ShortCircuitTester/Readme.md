# V1.9 Test介面優化

# V1.8 整體流程優化
1. 測試流程優化
建立 Step1 ~ Step12 完整流程
Step7~Step10 電壓異常仍完成後續測試
Step11 統一最終判定 OK / NG
Step12 自動返回 Step1
2. 條碼 / 序號管理
條碼格式驗證
重複測試警告提示
測試完成後自動 Focus 回條碼欄位
表格第一列顯示序號 / 條碼資訊
3. 電表量測控制
電阻量測流程整合
DC 電壓量測流程整合
新增 DC Voltage Range 設定
支援 Auto / 固定 Range
4. Step 等待時間設定
Step1~Step10 獨立 WaitMs
支援 Relay 延遲時間設定
Debug Log 顯示等待秒數
5. Retry 機制
電壓測試支援 Retry
Retry 不重複顯示多筆資料
保留 Retry 紀錄於 Log
6. 測試結果判定
PASS / FAIL 統計
最終結果統一判定
顯示測試完成狀態
7. 設備異常偵測
LAN 異常偵測
USB Relay 異常偵測
電表通訊異常偵測
異常立即停止測試並提示
8. 連線管理
LAN 手動連線 / 中斷
USB Relay 手動連線 / 中斷
拔線即時更新狀態
不自動重新連線
9. Settings 設定視窗
設備連線參數
條碼規則設定
電阻條件設定
電壓條件設定
電流條件設定
Step 等待時間設定
UI 執行參數設定
10. Test 頁面優化
Test / Settings / Debug Log 頁籤
測試步驟顯示優化
Range 欄位顯示
狀態顯示區優化
DataGridView 顯示優化
11. Debug Log
顯示 SCPI 指令
顯示 Relay 狀態
顯示 WaitMs
顯示 Retry 紀錄
顯示設備異常資訊
12. Config 管理
DX01Config.json 統一管理
支援設定儲存 / 載入
支援 DcVoltageRange 參數
支援 StepWaitMs 參數

# V1.2 UI Refactor 
1. 修正測試頁 DataGridView 跑版
2. 新增 SettingForm
3. Config 全部集中管理
4. LAN 預設
5. StepWaitMs 空白=0
6. 參數設定獨立視窗

# first upload V1.0
V1.0<br>
DX01_專案外殼短路判斷流程<br>