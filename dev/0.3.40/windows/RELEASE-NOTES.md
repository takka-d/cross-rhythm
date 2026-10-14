# Cross Rhythm 0.3.40 development

## Changes

- Songsのホバー効果音を表示領域単位でラッチし、画面再描画や内部IDの変化で連続再生されないように修正しました。
- コントローラーの仮想カーソルとPCマウスカーソルを相互排他にし、最後に操作した側へ表示を切り替えるようにしました。WebのHTML重畳ボタンにも同じ切替を反映しました。
- Windowsの起動画面を全画面から中央の16:9ウィンドウへ変更しました。DPIと小型画面に合わせて縮小し、文字の重なりを防ぎました。Closeはゲーム本体と同じ配色へ変更し、実クリックによる起動中止を検査しました。
- MIDI重複選択は一括選択を廃止し、各候補の元MIDI音、変換先、Track、Channel、Velocity、強弱を表示して、残す1音を直接選べるようにしました。
- Hi-Hat、Open Hi-Hat、Ride、Ride Cupの中強度を約4 dB補正しました。6段階の強弱差とSnowdomeのMIDI取込経路を検査しています。Ride Crashは変更していません。
- Undo/Redo中は大きな譜面の背景を先に再描画せず、軽い処理中画面を先に表示するようにしました。
- Playの中断選択をResume、Practice、Songsに統一し、PracticeからSongsへは確認画面なし、Startからは新しい採点Playへ移る既存動作を回帰検査へ固定しました。

## Verification

- Windows/WebのUnityビルドを完了し、各1,137件のコア・安定性・配置検査がPASSしました。
- Windows実行時検査は702件がPASSし、Snowdomeの強弱3、MIDI重複候補、Practice/Play遷移、Undo/Redoの処理中表示順を含めて確認しました。
- Launcherは中央16:9、非全画面、文字領域非重複、Close/Escape中止、二重起動防止、起動先への引継ぎを実動検査しました。
- Webブリッジ50件、独立MIDI解析23件、ペダル解析8件がPASSしました。
- Web実画面でTitle、Songs、Practice、Play中断、Edit、Undo復元を確認し、コンソールのerror/warningは0件でした。
- 公開後にGitHub Pages、Windows ZIP、SHA-256、公開ファイルを再照合します。

## Not verified with physical hardware

- 物理ゲームコントローラーでのカーソル切替と操作感
- HH/Rideの実際の聴感と、利用者の音量環境でのバランス
- 異なるDPIの実モニター間でWindows起動画面を移動した時の見た目
- OS標準ファイルダイアログを含む一連のコントローラー操作
- 大規模譜面を実画面から読み込んだ時のUndo/Redo進捗表示時間
