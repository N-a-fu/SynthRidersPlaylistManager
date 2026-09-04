# Synth Riders Playlist Manager

Steam 版 Synth Riders V3 以降を対象とする、Windows 用のローカル楽曲ライブラリ／Favorites／Playlist 管理アプリです。

現在は **Phase 2C（実Cover ArtのRead-only統合）** です。検出済み実環境では実曲・Favorites・Playlistと、確認できたhash対応Coverを表示し、全ゲームデータ書込操作を無効化します。Cover未設定は正常状態としてPlaceholderを表示します。Writerは実装していません。

## 方針

- Windows 10 / 11 x64、C# / .NET / WPF / MVVM
- ゲーム側データを Source of Truth とし、基本機能はオフラインで動作
- Steam 版 V3+ のみを対象
- 未確認の保存形式を推測せず、書き込み前に実データで検証
- 将来は GitHub Releases で self-contained Portable ZIP を配布

本プロジェクトは非公式ツールであり、Synth Riders または Kluge Interactive の公式製品ではありません。

Phase 2CのCover設計と実環境件数は [`docs/phase-2c-cover-art.md`](docs/phase-2c-cover-art.md)、Phase 2BのReader設計は [`docs/phase-2b-real-song-library.md`](docs/phase-2b-real-song-library.md)、環境検出は [`docs/phase-2a-environment-discovery.md`](docs/phase-2a-environment-discovery.md) を参照してください。
