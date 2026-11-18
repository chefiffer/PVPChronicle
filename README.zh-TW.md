# PvP Chronicle · 戰場紀事

[English](README.md) | [简体中文](README.zh-CN.md) | **繁體中文** | [한국어](README.ko-KR.md) | [Русский](README.ru-RU.md) | [Português (Brasil)](README.pt-BR.md) | [Español](README.es-ES.md) | [ไทย](README.th-TH.md) | [Tiếng Việt](README.vi-VN.md) | [日本語](README.ja-JP.md) | [Deutsch](README.de-DE.md) | [Français](README.fr-FR.md)

PvP Chronicle 是用於記錄和覆盤《最終幻想 XIV》PvP 戰局的 Dalamud 外掛。
它支援紛爭前線和水晶衝突的本地戰績、詳細覆盤、地圖機制、資料統計與具有
明確證據邊界的行為完整性複核。

## 安裝

1. 安裝並啟動 XIVLauncher，確認 Dalamud 已啟用。
2. 開啟 **Dalamud 設定 → 測試版**。
3. 新增下面的自定義外掛倉庫地址：

   ```text
   https://raw.githubusercontent.com/chefiffer/dalamud-plugins/main/repo.json
   ```

4. 開啟外掛安裝器，搜尋 **PVP Chronicle** 並安裝。

## 主要功能

- 紛爭前線與水晶衝突的本地戰局記錄和詳細覆盤。
- 比分、KDA、戰鬥流、死亡、移動、標點和可觀測地圖機制時間線。
- PvP 月曆、地圖輪換、地圖專屬機制與八語言本地化介面。
- 可選上傳戰局，以獲取伺服器生成的 Logs 和歷史參考分佈。

PvP Chronicle 不會自動操作遊戲、代替玩家釋放技能、揭示客戶端無法觀測的
隱藏狀態，也不會把不完整的觀測直接當作作弊定論。

## 參與貢獻

Fork 本倉庫，為單一改動建立分支，補充相應測試，然後向 `main` 提交 Pull
Request。

請勿提交真實戰局包、玩家資料、訪問令牌、私有伺服器地址或其他玩家的個人
資訊。安全問題請按照 [`SECURITY.md`](SECURITY.md) 私下報告。

## 許可證

本倉庫中的 PvP Chronicle 使用者端原始碼採用
[GNU Affero General Public License v3.0](LICENSE) 許可證。

該許可證僅適用於 PvP Chronicle 原創使用者端原始碼及明確標記為
AGPL-3.0-only 的其他檔案，不會把《最終幻想 XIV》素材、第三方程式庫或
字型重新授權為 AGPL；這些內容分別受其權利人和對應許可證約束。

《最終幻想 XIV》的名稱、影象和圖示等素材仍歸 Square Enix 所有。遊戲素材
署名及第三方許可證說明見
[`plugin/THIRD-PARTY-NOTICES.md`](plugin/THIRD-PARTY-NOTICES.md)。

PvP Chronicle 是獨立、非商業的社群專案，與 Square Enix 不存在隸屬或官方認可關係。
