# Lộ Trình Phát Triển FF3D (Game Mô Phỏng Phòng Cháy Chữa Cháy)

## Tầm Nhìn Sản Phẩm
Xây dựng game mô phỏng chữa cháy chiến thuật góc nhìn thứ nhất (Squad-based Tactical FPS). Người chơi vào vai chỉ huy tuyến đầu, tiếp nhận tin báo, đánh giá hiện trường, chỉ đạo đội AI (Bot), quản lý trang bị chuyên dụng, kiểm soát cháy lan và cứu nạn dưới áp lực thời gian.

## Hiện Trạng Dự Án (Đã hoàn thiện các hệ thống cốt lõi)
Dự án đã vượt qua giai đoạn nguyên mẫu và sở hữu một nền tảng mô phỏng phức tạp:
- **Hệ thống Call Phase (Tiếp nhận tin báo):** Mini-game tại bàn trực ban, phân tích hội thoại, đánh giá rủi ro và tạo Seed cho màn chơi (Random Incident).
- **Hệ thống Fire & Môi trường:** Mô phỏng cháy lan theo cụm (Cluster), tạo vết cháy (scorch decal), hệ thống khói ngạt, và báo cháy.
- **Hệ thống AI Đồng Đội (Bot Squad):** AI có khả năng nhận lệnh (dập lửa, di chuyển, theo sau, cứu người, dọn chướng ngại vật), tự quản lý kho đồ và phối hợp rải vòi nước.
- **Hệ thống Trang Bị (Tool Wheel):** Đã tích hợp đa dạng công cụ thực tế như vòi cứu hỏa, các loại bình xịt, rìu, camera ảnh nhiệt, mặt nạ oxy, thang dây, thang cứng, đệm cứu hộ, bộ sơ cứu.
- **Hệ thống Nhiệm vụ (Mission System):** Framework nhiệm vụ linh hoạt với các mục tiêu, điều kiện thắng/thua, chấm điểm và đánh giá hiệu suất.

## Lộ Trình Cập Nhật & Mở Rộng 

### Giai Đoạn 1 - Tối Ưu UX/UI & Đánh Bóng Core Loop
Mục tiêu: Làm mượt mà trải nghiệm từ lúc nhận tin báo đến khi hoàn thành nhiệm vụ.
- Cải thiện phản hồi hình ảnh/âm thanh khi ra lệnh cho AI Bot.
- Tối ưu hóa giao diện Tool Wheel và hệ thống Inventory để thao tác nhanh hơn trong các tình huống khẩn cấp.
- Cân bằng độ khó: Chỉnh sửa các thông số lan truyền của lửa, tốc độ hao hụt Oxy và cơ chế phạt (penalty) của Mission System.
- Đánh bóng Cutscene và màn hình tổng kết nhiệm vụ (Scoring/Snapshot).

### Giai Đoạn 2 - Mở Rộng Nội Dung Màn Chơi (Level Design)
Mục tiêu: Đưa các hệ thống đã xây dựng vào các môi trường đa dạng, phức tạp.
- Xây dựng thêm các Map chuyên biệt (nhà dân cư, khu công nghiệp, chung cư cao tầng).
- Thiết lập các Scenario (Kịch bản sự cố) đặc biệt: Rò rỉ hóa chất, nổ khí gas, cháy xe, điểm nóng bùng phát lại (backdraft/flashover).
- Tận dụng tối đa hệ thống Random Seed (Incident Generation) để mỗi lần chơi ở một Map đều có điểm phát cháy, số lượng và vị trí nạn nhân khác nhau.

### Giai Đoạn 3 - Nâng Cấp Hệ Thống AI & Hành Vi Nạn Nhân
Mục tiêu: Tăng tính chân thực, căng thẳng cho khâu cứu nạn cứu hộ.
- Thêm các trạng thái tâm lý/vật lý phức tạp cho nạn nhân (hoảng loạn bỏ chạy, nấp dưới gầm bàn, chống cự do sốc, bất tỉnh ngẫu nhiên).
- Nâng cấp pathfinding (hệ thống tìm đường) cho AI Đồng đội để xử lý các không gian hẹp, cầu thang và nhiều chướng ngại vật (debris) một cách mượt mà hơn.
- Thêm cơ chế giao tiếp bằng giọng nói (Voice lines) hoặc tín hiệu SOS giữa người chơi, nạn nhân và Bot.

### Giai Đoạn 4 - Tối Ưu Hiệu Suất & QA (Quality Assurance)
Mục tiêu: Đạt hiệu suất ổn định cho bản Release Candidate (RC).
- Tối ưu hóa hệ thống Batching cho cụm mô phỏng lửa (Fire Simulation Clusters) và hệ thống vết cháy (Scorch Decals) để giảm tải CPU/GPU.
- Viết thêm các bộ PlayMode Tests tự động cho hệ thống AI Bot Command và luồng thay đổi trạng thái của Mission.
- Tiến hành Playtest diện rộng (Bug bash) để tinh chỉnh độ nhạy điều khiển và sửa các lỗi tương tác kẹt đồ vật.