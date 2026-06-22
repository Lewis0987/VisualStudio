# V2.3 
1. 新增 Power ON/OFF 自動偵測功能
2. 新增 Power 門檻設定（ON/OFF Threshold） "EnablePowerCheckBypass": false 就能恢復正式檢測邏輯，不用再改程式碼。
3. 優化測試流程及自動化機制
4. CSV Log 欄位格式重構與量測資料完整化
5. 提升量產測試穩定性與操作便利性
6. 調整NG邏輯=>原Retry 3 次 FAIL → 改時間內持續量測NG才FAIL
7. 測試狀態【NG】邏輯改回立即停止模式

# V2.2 
Admin（預設 admin000 / admin000）
- 增加人員追溯能力
- 記錄每筆測試操作者
- 防止未授權修改參數
- 強化產線管理與稽核能力
- 新增帳號權限登入功能
- admin排查權限 LOG紀錄路徑【D:\VisualStudio\DX01_ShortCircuitTester\DX01_ShortCircuitTester\bin\Debug\net48\Logs】

# V2.1 優化內容
1. USB Relay 控制功能優化
新增 USB Relay 展開/收合控制、預設收合顯示、Tab 切換自動收合、版面配置與高度最佳化。
2. 條碼輸入與操作流程優化
新增條碼提示文字、自動聚焦輸入框、完成測試後自動返回條碼欄位，提升連續測試效率。
3. Power ON 檢查功能新增
新增產品開機狀態檢查機制，於測試開始前確認產品電壓是否符合條件。
4. 產品未開機警告功能優化
新增「略過」與「確定」選項，可選擇停止測試或忽略警告繼續執行。
5. 彈窗系統統一優化
統一所有警告、錯誤與提示視窗樣式，修正內容換行、按鈕排列與版面跑版問題。
6. 設備連線監控功能強化
強化 LAN 電表與 USB Relay 連線監控，即時偵測斷線並顯示異常狀態。
7. 異常處理與測試保護機制優化
設備異常或通訊失敗時立即停止測試，並以 UI 訊息提示取代程式例外畫面。
8. 測試流程與 Step 顯示優化
優化 Step 顯示資訊、量測值呈現方式及測試流程可讀性。
9. 測試結果與統計功能修正
修正 PASS／FAIL 顯示邏輯與統計計算異常問題。
10. Debug Log 與測試紀錄強化
完整記錄測試流程、設備連線事件及異常訊息，並保留測試結果資料。
11. Settings 設定管理優化
測試參數集中管理，支援各 Step 測試條件與參數設定。
12. 整體 UI 與操作體驗優化
統一介面風格、按鈕樣式、字體與控制項配置，提升操作流暢度與測試穩定性。

# V2.0 優化內容 
1. 修復 WinForms Designer 無法開啟問題
2. 完成 Barcode 輸入區重構
3. 新增 Barcode Placeholder 與格式驗證
4. 優化錯誤提示與紅框顯示
5. 強化 LAN / Relay 背景監控
6. 強化重複測試與連線防呆機制
7. 提升現場測試操作效率與穩定性

# V1.9 Test介面優化
>N/A

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