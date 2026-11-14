# PvP Chronicle

[English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [한국어](README.ko-KR.md) | [Русский](README.ru-RU.md) | [Português (Brasil)](README.pt-BR.md) | [Español](README.es-ES.md) | [ไทย](README.th-TH.md) | [Tiếng Việt](README.vi-VN.md) | **日本語** | [Deutsch](README.de-DE.md) | [Français](README.fr-FR.md)

PvP Chronicle は、『ファイナルファンタジーXIV』の PvP 試合を記録・振り返るための Dalamud プラグインです。フロントラインとクリスタルコンフリクトの履歴、リプレイ、マップ目標、パフォーマンス統計、そして観測可能な証拠の範囲に限定した行動整合性レビューに対応しています。

## インストール

1. Dalamud を有効にした XIVLauncher をインストールして起動します。
2. **Dalamud Settings → Experimental** を開きます。
3. 次のカスタムリポジトリ URL を追加します。

   ```text
   https://raw.githubusercontent.com/chefiffer/pvpchronicle/main/repo.json
   ```

4. プラグインインストーラーを開き、**Battlefield Frontline Collector** を検索して **Install** を選択します。

## 主な機能

- フロントラインとクリスタルコンフリクトのローカル試合履歴と詳細リプレイ。
- スコア、KDA、戦闘の流れ、戦闘不能、移動、マーカー、観測可能な目標のタイムライン。
- PvP カレンダー、マップローテーション、マップ固有ギミック、多言語 UI。
- サーバー生成の Logs と参照分布を取得するための任意アップロード。

PvP Chronicle はプレイを自動化せず、戦闘アクションを実行せず、隠されたゲーム状態を明らかにしません。また、不完全な観測を不正行為の確定的証拠として扱いません。

## コントリビューション

リポジトリをフォークし、対象を絞ったブランチを作成し、変更箇所に対応するテストを追加して、`main` 宛てにプルリクエストを作成してください。

試合キャプチャ、プレイヤーデータセット、アクセストークン、非公開サーバーのアドレス、他のユーザーの個人データは送信しないでください。セキュリティ上の問題は [`SECURITY.md`](SECURITY.md) に従って報告してください。

## ライセンス

このリポジトリの PvP Chronicle クライアントソースは [GNU Affero General Public License v3.0](LICENSE) でライセンスされています。

このライセンスは、PvP Chronicle 独自のクライアントソースと、AGPL-3.0-only と明示されたファイルにのみ適用されます。『ファイナルファンタジーXIV』の素材、第三者ライブラリ、フォントを再ライセンスするものではなく、それぞれの権利者とライセンス条件が引き続き適用されます。

『ファイナルファンタジーXIV』の名称、画像、アイコンは Square Enix に帰属します。ゲーム素材の帰属表示と第三者ライセンスについては [`plugin/THIRD-PARTY-NOTICES.md`](plugin/THIRD-PARTY-NOTICES.md) を参照してください。

PvP Chronicle は独立した非営利のコミュニティプロジェクトであり、Square Enix との提携または同社による承認を受けたものではありません。
