# 2026-05-29 工作交接
# 2026-06-02 接手

- 開發環境
  - Visual Studio 2026
    Community 2026

  - Visual Studio Code

### 開發中專案
 N/A
### 維護中專案

# FIA01 Shopfloor Local Control 
- GitLab Project
  - http://192.168.1.37/water.lin/fia01_localcontroller/-/tree/main/FIA01/fia01_localcontroller
- 本地端資料夾位置
  - D:\_vs_prj\FIA01\fia01_localcontroller
- 專案描述
  - 管理和控制生產工作站的應用程式。該應用程式使用 .NET Framework 4.7.2 開發，並包含多個工作站類別，用於處理不同類型的數據和任務。
  - 工站資料來源區分csv/socket，各工站依產出資料類型選舉繼承所屬類別。
  - config.ini記錄所有工站ip及socket port號。
- 佈署專案步驟
  - 使用 AnyDesk 應用程式，打開程式後輸入 `502092602/etic@13098982` 進入shopfloor伺服器。
  - 向產線確認已完全停線後，將原程式停止並關閉，沿用Config.ini及DB檔，
    將打包完的檔案丟進`C:\Users\shopfloor\Desktop\FIA01 LC v2.0`即上傳完畢。    

# FIA01 NG Station
- GitLab Project
  - http://192.168.1.37/water.lin/fia01_ng_workstation
- 本地端資料夾位置
  - D:\_vs_prj\FIA01\NG station\fia01_ng_workstation
- 專案描述
  - 生產過程出現的NG品，記下發生原因並建表追綜。
- 佈署專案步驟
  - 使用 AnyDesk 應用程式，打開程式後輸入 `1505693500/etic@13098982` 進入NG 工站主機。
  - 關閉程式並以桌面的程式捷徑開啟目標檔案位置。
  - 沿用原報表存檔資料夾，將打包完的檔案丟進完成更新。    

# FIA01 裝箱秤重
- GitLab Project
  - http://192.168.1.37/hero.liu/fia01_scale/-/tree/fia01_update
- 本地端資料夾位置
  - D:\_vs_prj\FIA01\Package\fia01_scale\Etica Scale
- 專案描述
  - 成品裝箱秤重，具備跨站及淨重重量檢查。
- 佈署專案步驟
  - 使用 FileZilla 應用程式，點選左上角檔案下方的站台管理員，輸入以下資訊：
    主機：`192.168.28.93:5893`
    使用者名稱：`FIA01`
    密碼：`Etic@20262893#$`
    連線成功後以桌面的程式捷徑開啟目標檔案位置，將打包後的檔案上傳至該資料夾並沿用_setup.ini。

# FIA01 打棧板工站
- GitLab Project
  - -	http://192.168.1.37/water.lin/fia01_pallet
- 本地端資料夾位置
  - D:\_vs_prj\FIA01\Pallet\FIA01 Pallet
- 專案描述
  - 記錄棧板放置幾箱，以第1箱號為棧板號
- 佈署專案步驟
  - 使用 AnyDesk 應用程式，打開程式後輸入 `827327409/etic@13098982` 進入打棧板 工站主機。
  - 關閉程式並以桌面的程式捷徑開啟目標檔案位置。
  - 沿用原報表存檔資料夾，將打包完的檔案丟進完成更新。

# FIA01 OBA工站
- GitLab Project
  - http://192.168.1.37/water.lin/fia01_scale_oba
- 本地端資料夾位置
  - D:\_vs_prj\FIA01\Scale_OBA\Scale_OBA
- 專案描述
  - 品保抽測成品，檢驗出貨標準。
- 佈署專案步驟
  - 使用 AnyDesk 應用程式，打開程式後輸入 `1505693500/etic@13098982` 進入OBA 工站主機。
  - 關閉程式並以桌面的程式捷徑開啟目標檔案位置。
  - 沿用原報表存檔資料夾，將打包完的檔案丟進完成更新。  

# REO01 Shopfloor Local Control 
- GitLab Project
  - http://192.168.1.37/water.lin/reo01_localcontroller
- 本地端資料夾位置
  - D:\_vs_prj\REO01\REO01_localcontroller
- 專案描述
  - 管理和控制生產工作站的應用程式。該應用程式使用 .NET Framework 4.7.2 開發，並包含多個工作站類別，用於處理不同類型的數據和任務。
- 佈署專案步驟
  - 使用 AnyDesk 應用程式，打開程式後輸入 `1998175356/etic@13098982` 進入shopfloor伺服器。
  - 向產線確認已完全停線後，將原程式停止並關閉，沿用Config.ini及DB檔，
    將打包完的檔案丟進`C:\Users\user\Desktop\REO01 LC V1.14`即上傳完畢。

# REO01
- GitLab Project
  - http://192.168.1.37/water.lin/reo01
- 本地端資料夾位置
  - D:\_vs_prj\REO01\code
- 專案描述
  - 工站1~7共用同支程式，分屬不同功能，工站只顯示單1頁籤，第7站資料未上傳shopfloor。
- 佈署專案步驟
  - 使用 AnyDesk 應用程式，打開程式後輸入站號及共同密碼 `etic@13098982` 進入各工站主機，連線站號如下：
    station1 電芯分選,10.10.32.185\station1_cell,anydesk: 451843650
    station2 M/C綁定,10.10.32.182\Station2_MC,anydesk: 1591055610/1903137812
    station3 鋁排焊後,10.10.32.188\Station3_ModuleCellVR,anydesk: 1713245473
    station4 大小板綁定,10.10.32.190\Station4_mPCBAboard,anydesk: 405848539
    station5 PCBA焊後,10.10.32.191\Station5_PcbaCellir,anydesk: 1418261801
    station6 Pack組裝,10.10.32.195\Station6_PM,anydesk: 1211169734/1140707281
  - 關閉程式並以桌面的程式捷徑開啟目標檔案位置。
  - 沿用原報表存檔資料夾及config.ini，將打包完的檔案丟進完成更新。 

# REO01 
- GitLab Project
  - http://192.168.1.37/lewis.chiu/reo01.git
- 專案描述
  - 原工站station4 大小板綁定、station5 PCBA焊後站合併為【station4 PCBA綁定測試站】



# DTX01 PCBA工站
- GitLab Project
  - http://192.168.1.37/water.lin/2nd-pcba-sn-burnin
- 本地端資料夾位置
  - D:\_vs_prj\DTX01\2nd-pcba-sn-burnin
- 專案描述
  - 以樹莓派運行py程式。
  - PCBA上的2保參數燒入程式，參數位置/conf/xx.csv，csv檔案由研發部提供。

# DTX01 總檢站 Final Check
- GitLab Project
  - http://192.168.1.37/water.lin/finalcheck
- 本地端資料夾位置
  - D:\_vs_prj\DTX01\FinalCheck
- 專案描述
  - 編碼原則/重碼檢查，NG不記錄。
- 佈署專案步驟
  - 使用 AnyDesk 應用程式，打開程式後輸入 `334978172/etic@13098982` 進入工站主機。
  - 關閉程式並以桌面的程式捷徑開啟目標檔案位置。
  - 沿用原報表存檔資料夾，將打包完的檔案丟進完成更新。

# DTX01 NG Station
- GitLab Project
  - http://192.168.1.37/kidian/dtx01_ng_workstation/-/tree/master
- 本地端資料夾位置
  - D:\_vs_prj\DTX01\dtx01_ng_workstation
- 專案描述
  - 生產過程出現的NG品，記下發生原因並建表追綜。
- 佈署專案步驟
  - 使用 AnyDesk 應用程式，打開程式後輸入 `1498259507/etic@13098982` 進入NG 工站主機。
  - 關閉程式並以桌面的程式捷徑開啟目標檔案位置。
  - 沿用原報表存檔資料夾，將打包完的檔案丟進完成更新。

# DTX01 OBA Station
- GitLab Project
  - http://192.168.1.37/water.lin/oba_vr
- 本地端資料夾位置
  - D:\_vs_prj\OBA VR record
- 專案描述
  - 品保抽測成品，檢驗出貨標準。
- 佈署專案步驟
  - 使用 AnyDesk 應用程式，打開程式後輸入 `1260906384/etic@13098982` 進入OBA 工站主機。
  - 關閉程式並以桌面的程式捷徑開啟目標檔案位置。
  - 沿用原報表存檔資料夾，將打包完的檔案丟進完成更新。 

# DTX01 成品裝箱
- GitLab Project
  - http://192.168.1.37/water.lin/packagesn
- 本地端資料夾位置
  - D:\_vs_prj\DTX01\PackageSN\PackageSN
- 專案描述
  - 外箱、配件包序號掃瞄綁定並檢查編碼原則。
- 佈署專案步驟
  - 使用 AnyDesk 應用程式，打開程式後輸入 `705150848/etic@13098982` 進入OBA 工站主機。
  - 關閉程式並以桌面的程式捷徑開啟目標檔案位置。
  - 沿用原報表存檔資料夾，將打包完的檔案丟進完成更新。 

# DTX01 Pack、Cell 序號綁定
- GitLab Project
  - http://192.168.1.37/water.lin/pack_cell_sn
- 本地端資料夾位置
  - D:\_vs_prj\DTX01\PACK_CELL_SN\Pack_cell_SN
- 專案描述
  - Pack綁定45個cell序號，api取得電芯分選資料並作電壓差比較。
- 佈署專案步驟
  - 使用 AnyDesk 應用程式，打開程式後輸入 `1591055610/etic@13098982` 進入OBA 工站主機。
  - 關閉程式並以桌面的程式捷徑開啟目標檔案位置。
  - 沿用原報表存檔資料夾，將打包完的檔案丟進完成更新。 

# DTX01 電壓/內阻量測
- GitLab Project
  - http://192.168.1.37/water.lin/pack_vr
- 本地端資料夾位置
  - D:\_vs_prj\Pack VR record
- 專案描述
  - 量測成品的電壓內阻是否符合出貨標準。
  - LimitModify.log及thresholds.txt為隱藏檔。
- 佈署專案步驟
  - 使用 AnyDesk 應用程式，打開程式後輸入 `/etic@13098982` 進入電壓內阻量測工站主機。
  - 關閉程式並以桌面的程式捷徑開啟目標檔案位置。
  - 沿用原報表存檔資料夾，將打包完的檔案丟進完成更新。 

# DTX01 倉庫入料檢
- GitLab Project
  - http://192.168.1.37/water.lin/prod_history
- 本地端資料夾位置
  - D:\_vs_prj\DTX01\Prod_history\Prod_history
- 專案描述
  - 5大類入庫檢查，不可重碼/混料。
  - 掃瞄本機自存db檔，掃完後需自行執行上傳到後台。
- 佈署專案步驟
  - 使用 AnyDesk 應用程式，打開程式後輸入 `/etic@13098982` 進入土城倉管的電腦。
  - 關閉程式並以桌面的程式捷徑開啟目標檔案位置。
  - 沿用原報表存檔資料夾，將打包完的檔案丟進完成更新。 

# DTX01 外殼短路判斷 ShortCircuitTester
- GitLab Project
  - http://192.168.1.37/lewis.chiu/dtx01_casing_short_circuit.git
- 本地端資料夾位置
   - D:\VisualStudio\DX01_ShortCircuitTester
- 專案描述
  - 連線設備LAN IP:192.168.100.100
  - CI/CD自動包版 main commitID: 698b1935
  - G1 / G2 / P+ / P- 量測導通/電阻/電壓數值正確性，檢測流程Step1 ~ Step10

# R12H UI
- GitLab Project
  - http://192.168.1.37/water.lin/r12h_ui
- 本地端資料夾位置
  - D:\_vs_prj\R12_UI\R12_UI
- 專案描述
  - IPC IP:192.168.127.100:502,連線後讀取設備資料/秒

# ZFO01 UI
- GitLab Project
  - http://192.168.1.37/water.lin/zfo01_ui
- 本地端資料夾位置
  - D:\_vs_prj\ZFO01_UI\ZFO01_UI
- 專案描述
  - 控制智帆20呎櫃的高壓箱上下電/PreCharge/DC Switch/PCS
  - IPC IP:192.168.127.100:502,連線後讀取設備資料/秒
  - PCS IP:192.168.127.231:502,連線後讀取設備資料/秒
  - 設定PCS微電網時的輸出頻率
  - 警告/保護點狀態讀取

# SinexcelPCS Control Panel
- GitLab Project
  - http://192.168.1.37/water.lin/sinexcelpcs
- 本地端資料夾位置
  - D:\_vs_prj\SinexcelPCScontrol\SinexcelPCS
- 專案描述
  - RS485與盛弘pcs連線，9600 N81，使用MODBUS RTU指令。
  - 控制啟/停機，設定充/放電及kw值，讀回關鍵狀態值並做為動作憑據。

# SQLite DB Editor
- GitLab Project
  - http://192.168.1.37/water.lin/sqlite-editor
- 本地端資料夾位置
  - D:\_vs_prj\SQLite Editor\code\SQLiteEditor
- 專案描述
  - DB檔維護用工具，未釋出。
  - 先預覽、後執行，預設啟用 Auto Backup
  - 資料修改（Edit Selected）：Preview 中選取的一筆資料，支援修改欄位 JsonData、Uploaded

# 三重廠即時用電曲線圖 PrintMeterWeb
- GitLab Project
  - http://192.168.1.37/water.lin/printmeterweb
- 本地端資料夾位置
  - D:\_vs_prj\print_meter_web
- 專案描述
  - 讀取廠內電表及2台獨立櫃充放電數據以網頁方式呈現
  - 大廳電腦執行程式，使用VNC連線192.168.1.47(user/13098982)
  - 即時電力監控網頁：http://192.168.1.47:6160/
  - 執行 app.py，主結構index.html

# DC Loader control
- GitLab Project
  - http://192.168.1.37/water.lin/dcloader/-/tree/main/DCloader
- 本地端資料夾位置
  - D:\_vs_prj\DCloader
- 專案描述
  - 設定好截止條件，啟動 DC Loader 34000 執行放電。
  - 通訊：RS232 9600/N81
  - 截止條件：電壓/電流/時間
  - 可設定抽載電流

# 獨立櫃 250kw - 統益案場
- 連線方式
  - 先VNC連線到1樓大廳電腦A (192.168.1.47 , user/13098982)
  - 再以A的瀏覽器輸入 http://125.227.108.239:8853/ 進行連線，無需帳密。

- 專案描述
  - 台電電網、電櫃、PV(0~70KW)與廠內負載(2~60KW)併接使用。
  - 獨立櫃孤島電網受PV發電干擾,加裝隔離變壓器改善。