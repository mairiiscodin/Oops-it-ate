# Oops It Ate — Project Context

> Cập nhật lần cuối: 2026-09-01  
> Unity: `6000.4.0f1`  
> Mục đích: đây là file đầu tiên agent mới phải đọc trước khi sửa project.

## 1. Hướng dẫn bàn giao cho agent mới

Trước khi thay đổi code:

1. Đọc hết file này.
2. Đọc các tài liệu được liên kết ở mục 12 nếu công việc liên quan.
3. Kiểm tra `git status` và không ghi đè thay đổi chưa commit của người dùng.
4. Đối chiếu tài liệu với code hiện tại. Nếu có khác biệt, code đang chạy là bằng chứng kỹ thuật; phải báo rõ khác biệt trước khi thay đổi luật gameplay.
5. Nếu sửa thuật toán gameplay, cập nhật testcase Markdown tương ứng trong cùng thay đổi.

Không được giả định agent có quyền truy cập lịch sử chat hoặc tài khoản Codex/ChatGPT trước đó.

## 2. Tổng quan game

Đây là game puzzle theo ô lưới. Player lấy thức ăn từ oven, sau đó cho Pet/Oven/Box/Door/Border ăn để làm chúng phồng hoặc đẩy không gian xung quanh.

Các scene trong Build Settings:

```text
StartMenu
IntroCutscene
1
2
3
4
5
```

Gameplay level `1` đến `5` dùng `LevelSceneSettings` và được dựng lúc chạy bởi `SceneLevelBuilder`.

## 3. Điều khiển hiện tại

```text
W / Up Arrow     : đi Bắc
S / Down Arrow   : đi Nam
A / Left Arrow   : đi Tây
D / Right Arrow  : đi Đông
J                 : tương tác / lấy thức ăn / cho ăn
R                 : reset room hiện tại về trạng thái ban đầu
```

Reset bằng `R` xóa state theo scope của room hiện tại (vị trí Pet/Box, Pet đã ăn,
door đã unlock và animation hoàn thành), bỏ food đang cầm rồi reload scene. State của
các room khác trong cùng tiến trình chơi được giữ nguyên.

Player hiện chỉ nhận input di chuyển 4 hướng. Thuật toán **phồng và đẩy** vẫn hỗ trợ đủ 8 hướng.

File liên quan: `Assets/Scripts/Input/KeyboardGridInput.cs`.

## 4. Tile map và hệ tọa độ

Dòng đầu tiên của `tileMap` là hàng trên cùng, tức có `Y` lớn nhất.

```text
NW (-1,+1)   N (0,+1)   NE (+1,+1)
 W (-1, 0)      X         E (+1, 0)
SW (-1,-1)   S (0,-1)   SE (+1,-1)
```

Ký hiệu tile map:

| Ký hiệu | Ý nghĩa |
|---|---|
| `.` | Ô sàn |
| `#` | Tường cố định bên trong map |
| `_` | Border |
| `S` | Player start |
| `K` | Kitchen/Oven |
| `P` | Pet |
| `B` | Pushable box |
| `1`–`9` | Door marker; cũng được đọc như border |

`GridSettings.GridToWorld` đặt tâm map tại world `(0,0)` dựa trên `width`, `height` và `cellSize`.

File liên quan:

- `Assets/Scripts/Levels/LevelSceneSettings.cs`
- `Assets/Scripts/Grid/GridSettings.cs`
- `Assets/Scripts/Grid/GridPosition.cs`

## 5. Mô hình Pet và Oven

Oven không có thuật toán body riêng. `KitchenStation` gắn hoặc tạo một `PetBody` rồi cấu hình sprite oven cho body đó.

Vì vậy:

- Pet và Oven dùng chung thuật toán phồng.
- Pet có thể đẩy Oven.
- Oven có thể đẩy Pet.
- Cả hai dịch chuyển nguyên khối khi bị đẩy.
- `canBePushedByBodyGrowth` hiện bật cho Pet trong các scene; `KitchenStation.Initialize` chủ động bật cho Oven.

Box thường là `PushableBox`. Khi được cho ăn, box có thể chuyển thành một `PetBody` growable.

File liên quan:

- `Assets/Scripts/Actors/PetBody.cs`
- `Assets/Scripts/Interaction/KitchenStation.cs`
- `Assets/Scripts/Actors/PushableBox.cs`

## 6. Thuật toán phồng hiện tại

Điểm vào chính:

```text
PlayerInteractor.TryFeed
    -> BodyGrowthPlanner.TryBuild
    -> BodyGrowthPlan.TryCommit
    -> PetBody.TryGrow
```

Một lần ăn tạo tối đa một growth layer mới quanh toàn bộ body hiện tại:

- Xét 8 hướng quanh mỗi ô body.
- Không thêm ô đã thuộc body.
- Không thêm ô terrain bị chặn.
- Các target trùng nhau được gộp thành một candidate.
- Candidate trống có thể được chấp nhận trực tiếp.
- Candidate có actor phải lập được kế hoạch đẩy actor đó.
- Cho phép **partial growth**: một nhánh thất bại không hủy các nhánh độc lập khác.
- Nếu không có candidate nào được chấp nhận, không phồng và không mất thức ăn.

Food chỉ bị trừ sau khi plan commit thành công.

File liên quan:

- `Assets/Scripts/Actors/BodyGrowthPlanner.cs`
- `Assets/Scripts/Actors/BodyGrowthPlan.cs`
- `Assets/Scripts/Interaction/PlayerInteractor.cs`

## 7. Áp lực và thứ tự fallback

Nếu nhiều ô của body cùng gây lực lên một actor, các vector áp lực được cộng lại rồi lấy dấu từng trục:

```text
direction = (Sign(totalX), Sign(totalY))
```

Nếu kết quả là lực chéo, Pet/Oven/Box thử theo thứ tự bắt buộc:

| Lực tổng | Thứ tự thử |
|---|---|
| NE `(1,1)` | NE → E → N |
| NW `(-1,1)` | NW → W → N |
| SE `(1,-1)` | SE → E → S |
| SW `(-1,-1)` | SW → W → S |

Quy tắc tổng quát:

```text
chéo -> ngang -> dọc
```

Nếu hướng chéo bị lỗi cục bộ hoặc chỉ phát hiện xung đột sau khi ghép toàn bộ plan, nhánh phải thử hướng ngang rồi hướng dọc. Mỗi lần đổi hướng phải xóa các request đẩy dở của lần thử trước.

Nếu lực chỉ có một trục thì không fallback sang trục khác. Nếu tổng lực là `(0,0)`, actor không bị đẩy và candidate đang bị nó chiếm không được chấp nhận.

### Player là ngoại lệ

Player thử hướng áp lực trước. Nếu không hợp lệ, player có thể tìm hướng thoát trong đủ 8 hướng, ưu tiên hướng có tích vô hướng lớn hơn với áp lực. Tie-break cố định:

```text
N, NE, E, SE, S, SW, W, NW
```

Không áp dụng thuật toán né đủ 8 hướng của player cho Pet/Oven/Box.

## 8. Đẩy dây chuyền và transaction

Một nhánh có thể đẩy dây chuyền qua Box, Pet, Oven và Player.

Các luật bắt buộc:

- Actor nhiều ô dịch nguyên khối đúng một ô, không biến dạng.
- Actor phía xa phải được commit trước actor phía gần body đang phồng.
- Không được kết thúc hai actor trên cùng một ô.
- Một actor không được nhận hai hướng di chuyển khác nhau trong cùng transaction.
- Không cho phép chu trình đẩy.
- Không đẩy actor vào terrain, body đang phồng hoặc candidate growth đã đặt trước.
- Plan được kiểm tra trước khi thay đổi world.
- Nếu `TryGrow` cuối cùng thất bại hoặc có exception, các move đã áp dụng phải revert theo thứ tự ngược lại.

Trong một lần cho ăn chỉ target body phồng. Body khác chỉ bị dịch chuyển; không có hai body cùng phồng trong một transaction.

## 9. Luật đường chéo

Chuyển động hoặc growth chéo từ `A` tới `D` chỉ hợp lệ nếu đích và hai ô cạnh của đường chéo hợp lệ sau khi áp dụng plan:

```text
S D
A S

A = source
D = destination chéo
S = hai side cell
```

Ví dụ đi NE:

```text
destination = (x+1, y+1)
sideX       = (x+1, y)
sideY       = (x,   y+1)
```

Không được xuyên qua góc tường. Một side cell có actor chỉ hợp lệ nếu actor đó được plan đẩy ra và không còn chiếm side cell sau move.

## 10. Tường và border: hai sprite South

Không dùng autotile 16 trạng thái cho tường hoặc border. Mỗi loại hiện chỉ có hai sprite:

```text
wallSouthOpen
wallSouthClosed
borderSouthOpen
borderSouthClosed
```

Quy tắc tường:

```text
ô South là authored wall # -> Wall South Closed
ngược lại                  -> Wall South Open
```

Quy tắc border:

```text
ô South là border -> Border South Closed
ngược lại         -> Border South Open
```

Tường chỉ xét tường; border chỉ xét border. Hai loại không đóng South của nhau.

Asset theme hiện dùng:

```text
Assets/Assets/Grid Tile Theme.asset
```

Cả bốn field South hiện đã có sprite được gán. Nếu một field bị thiếu, getter tạm fallback sang sprite còn lại cùng loại để tile không bị vô hình.

File liên quan:

- `Assets/Scripts/Grid/GridTileTheme.cs`
- `Assets/Scripts/Grid/GridCellView.cs`
- `Assets/Docs/WallAutotilingGuide.md`

## 11. Camera gameplay

Camera của các gameplay level tự fit theo bounds thật của loaded cells và border:

```text
vertical requirement   = paddedHeight / 2
horizontal requirement = paddedWidth / (2 * camera.aspect)
orthographicSize       = max(vertical, horizontal)
```

Padding hiện là `0.5` cell mỗi phía.

Sau đó camera được **snap pixel-perfect**: số pixel màn hình mỗi cell được làm tròn xuống thành
bội số nguyên của kích thước texel tile (`tileTheme.floor`, hiện 32px), và vị trí camera được
snap để mép tile nằm đúng trên pixel. Không có bước này, zoom lẻ (nhất là sau khi đẩy border)
gây sọc ngang/dọc giữa các tile. Hệ quả: map có thể nhỏ hơn màn hình một chút so với fit thuần.

Camera phải refit khi:

- Level được dựng.
- Tỉ lệ Game View hoặc độ phân giải thay đổi.
- Border được đẩy ra.
- Border được phục hồi.

Không thay camera riêng của `StartMenu` và `IntroCutscene`.

File liên quan:

- `Assets/Scripts/Grid/GridWorld.cs`
- `Assets/Scripts/Levels/SceneLevelBuilder.cs`
- `Assets/Scripts/Levels/LevelRoot.cs`

## 12. Tài liệu chi tiết

Đọc khi sửa thuật toán phồng/đẩy:

- [`Assets/Docs/EightDirectionGrowthTestCases.md`](EightDirectionGrowthTestCases.md)

Đọc khi sửa hình tường/border:

- [`Assets/Docs/WallAutotilingGuide.md`](WallAutotilingGuide.md)

Nếu thay đổi một luật được mô tả trong các file trên, phải cập nhật tài liệu cùng lúc với code.

## 13. Quy trình chỉnh level trong Unity

`LevelSceneSettingsEditor` cung cấp level painter và các nút hỗ trợ:

- `Sync Objects From Map`
- `Add Door Scenes To Build`
- `Setup Pet/Kitchen Visuals Like Scene 1`

Menu chính:

```text
Tools/Oops It Ate/Open Level Painter
```

Sau khi sửa `tileMap`, sync object từ map trước khi đánh giá scene. Không chỉnh thủ công object sinh từ marker rồi quên đồng bộ lại map.

File liên quan:

- `Assets/Scripts/Editor/LevelSceneSettingsEditor.cs`
- `Assets/Scripts/Editor/SceneLevelMenu.cs`

## 14. Trạng thái lưu game giữa các room

`GameSession` giữ state tĩnh trong lúc chạy:

- Food player đang cầm.
- Door đã unlock.
- Vị trí Pet/Box đã lưu theo room và object ID.
- Thông tin door đến khi chuyển scene.
- Các flag gameplay.

State reset bởi `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` khi bắt đầu game mới/domain runtime mới. Đây chưa phải hệ thống save ra ổ đĩa.

File liên quan: `Assets/Scripts/Levels/GameSession.cs`.

## 15. Animation hoàn thành khi tất cả Pet đã ăn

Gameplay level tự theo dõi trạng thái đã ăn của các Pet (không tính Oven). Khi Pet cuối cùng
được cho ăn thành công, `RoomCompletionAnimation` phát các frame `OopsIAte` ở giữa camera,
tạm đặt `Time.timeScale = 0` và khóa `KeyboardGridInput`. Animation dùng realtime nên vẫn chạy
khi gameplay pause; hết animation sẽ phục hồi time scale trước đó và bật lại input. Nếu lần ăn
cuối đang đẩy Player, animation đợi movement visual kết thúc rồi mới pause và xuất hiện.

Trạng thái `HasBeenFed` của từng Pet được lưu theo room và tên object trong `GameSession` khi
chuyển scene. Khi quay lại room trong cùng tiến trình chơi, Pet đã ăn được khôi phục ở trạng thái
1x1 FatDog và vẫn được tính là đã ăn. Animation hoàn thành không phát lại khi quay lại room đã
hoàn thành. `StartNewGame` xóa các trạng thái này cùng session còn lại.

File liên quan:

- `Assets/Scripts/Levels/RoomCompletionAnimation.cs`
- `Assets/Resources/OopsItAteCompletionFrames.asset`
- `Assets/Scripts/Levels/GameSession.cs`

## 15b. Mini map tiến trình

Mỗi gameplay level tạo `MiniMapHud` (Canvas overlay góc trên phải, sorting order 100,
nằm dưới iris transition). Sơ đồ phòng đọc từ `Assets/Resources/MiniMapLayout.asset`.

- Tạo/cập nhật layout: `Tools/Oops It Ate/Generate Mini Map Layout`. Tool mở từng scene
  gameplay trong Build Settings, đọc các `DoorExit` và đặt phòng đích theo phía cửa nằm
  trên map (BFS từ scene `1`). Có thể sửa `cell` bằng tay sau khi generate; chạy lại tool
  sẽ ghi đè rooms/links nhưng giữ phần style.
- Trạng thái ô phòng: chưa vào nhưng kề phòng đã vào → tối; đã vào → xám; mọi Pet đã ăn
  (`GameSession.IsRoomCompleted`) → sáng, nhấp nháy một lần lúc vừa hoàn thành. Phòng
  hiện tại có khung trắng. `revealAllRooms` hiện toàn bộ map ngay từ đầu.
- `GameSession.RoomProgressChanged` báo khi vào phòng, hoàn thành hoặc reset phòng. Reset
  bằng `R` xóa trạng thái hoàn thành của room đó nên ô phòng tắt lại.

File liên quan:

- `Assets/Scripts/Levels/MiniMapHud.cs`
- `Assets/Scripts/Levels/MiniMapLayout.cs`
- `Assets/Scripts/Editor/MiniMapLayoutGenerator.cs`

## 15c. FTUE onboarding phòng 1

Bật bằng `LevelSceneSettings.playOnboarding` (hiện chỉ bật ở scene `1`).
`SceneLevelBuilder` thêm `OnboardingController`:

1. Icon WASD trên đầu player cho tới khi đi được 2 ô.
2. Oven bốc khói cho tới lần đầu lấy food; icon J hiện khi đứng sát, quay mặt vào oven và chưa cầm food.
3. Pet chưa ăn có bong bóng suy nghĩ món ăn (sau bước 1); icon J hiện khi cầm food và quay mặt vào Pet.
4. Cửa có `DoorExit.hiddenUntilRoomComplete` trông như border/tường, không đi qua và không cho ăn được.
   Cửa chỉ hiện (pop + rung) khi `RoomCompletionAnimation.Finished` báo animation "Oops I Ate" đã xong,
   và hiện sẵn khi quay lại room đã hoàn thành. Reset `R` làm cửa ẩn lại.

Art hint hiện là placeholder vẽ bằng code (`OnboardingArt`). Thay bằng art thật: đặt sprite tên
`KeyWASD`, `KeyJ`, `ThoughtBubble`, `Smoke` vào `Assets/Resources/Onboarding/`.

File liên quan: `Assets/Scripts/Onboarding/*.cs`, `Assets/Scripts/Levels/DoorExit.cs`,
`Assets/Scripts/Levels/LevelExitController.cs`.

## 15d. Âm thanh

`SoundManager` (`Assets/Scripts/Audio`) tự tạo khi game chạy, sống qua các scene
(`DontDestroyOnLoad`). Gọi `SoundManager.Play(Sfx.X, pitch, volume, delay)`.

- SFX mặc định được tổng hợp bằng code (`ProceduralSfx`, kiểu 8-bit). Thay bằng file thật:
  đặt clip tên trùng enum `Sfx` vào `Assets/Resources/Audio/Sfx/` (ví dụ `Eat.wav`).
- Nhạc nền: đặt clip `Assets/Resources/Audio/Music` (loop). Chưa có thì không phát nhạc.
- Âm lượng: `SoundManager.SfxVolume`/`MusicVolume`, lưu bằng PlayerPrefs; chưa có UI chỉnh.
- Mix từng âm (volume, pitch jitter, khoảng cách tối thiểu giữa 2 lần phát) nằm trong bảng `mix`.
- Điểm phát: bước đi/đâm tường (`GridMover`), lấy food (`PlayerInventory`), cho ăn/phồng/đẩy/không
  ăn được (`PlayerInteractor`), ợ (`PetBody`, `DoorExit`, `GridWorld`), cửa hiện (`DoorExit.Reveal`),
  qua cửa (`LevelExitController`), hoàn thành phòng (`RoomCompletionAnimation`), reset R.

## 16. Checklist kiểm tra trước khi bàn giao

- [ ] Project mở bằng Unity `6000.4.0f1`.
- [ ] Không có compiler error trong Console.
- [ ] Chạy gameplay level liên quan trong Play Mode.
- [ ] Cho Pet ăn và cho Oven ăn đều dùng cùng luật growth.
- [ ] Kiểm tra lực chéo fallback đúng `chéo -> ngang -> dọc`.
- [ ] Kiểm tra một chain push và thứ tự actor xa đi trước.
- [ ] Kiểm tra player không bị kẹt nếu còn hướng thoát hợp lệ.
- [ ] Kiểm tra đường chéo không xuyên qua góc.
- [ ] Kiểm tra partial growth khi một nhánh bị chặn.
- [ ] Kiểm tra food chỉ mất sau commit thành công.
- [ ] Kiểm tra Wall/Border South Open và Closed trong Inspector.
- [ ] Kiểm tra camera ở ít nhất một tỉ lệ ngang và một tỉ lệ hẹp/dọc.
- [ ] Kiểm tra `git diff` chỉ chứa thay đổi đúng phạm vi.
- [ ] Cập nhật file context/testcase nếu quyết định gameplay thay đổi.

## 17. Prompt gợi ý khi chuyển sang agent khác

```text
Hãy đọc toàn bộ Assets/Docs/PROJECT_CONTEXT.md trước, sau đó đọc các tài liệu mà file đó liên kết cho phần việc liên quan. Kiểm tra git status và code hiện tại trước khi sửa; không giả định lịch sử chat cũ có sẵn. Khi thay đổi luật gameplay, cập nhật cả code và testcase Markdown tương ứng, rồi kiểm tra trong Unity 6000.4.0f1.
```

