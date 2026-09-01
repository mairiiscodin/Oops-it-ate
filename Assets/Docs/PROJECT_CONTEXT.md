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

