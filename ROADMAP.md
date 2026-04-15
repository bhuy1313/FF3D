# Lo Trinh Phat Trien FF3D (Game Mo Phong Phong Chay Chua Chay)

## Tam Nhin San Pham
Xay dung game mo phong chua chay goc nhin thu nhat, noi nguoi choi danh gia hien truong, chon dung cong cu, kiem soat chay lan, cuu nan va hoan thanh nhiem vu duoi ap luc thoi gian va an toan.

## Hien Trang Du An (Thang 2/2026)
- He FPS (di chuyen, tuong tuong, inventory, vitals) da choi duoc.
- He lua da co nen tang (cuong do, chay lan, dap lua, gay sat thuong).
- Cong cu chinh da co (voi cuu hoa, binh chua chay, riu), nhung chieu sau gameplay con han che.
- Build settings da duoc cau hinh voi cac scene chinh (BootIntro, MainMenu, Map1, v.v.).

## Roadmap 2026

### Giai Doan 1 - Vertical Slice Choi Duoc (Thang 2-3/2026)
Muc tieu: Hoan thien mot vong lap nhiem vu day du, choi tron ven tu dau den cuoi.

Pham vi:
- Hoan thien luong chuyen scene tu Main Menu vao Map1.
- Them luong nhiem vu: briefing, dang lam nhiem vu, thang/thua, choi lai.
- Them muc tieu ro rang: dap cac diem chay, cuu nan nhan, thoat khu vuc an toan.
- Them UI theo doi tien do nhiem vu.
- Can bang bo cong cu hien co de tao khac biet ro rang.
- Bo sung audio phan hoi co ban cho lua, nuoc va trang thai nguy hiem.

Tieu chi hoan thanh:
- Nguoi choi hoan thanh duoc 1 nhiem vu tu dau den cuoi trong 1 phien choi.
- Trang thai thang/thua ro rang, on dinh.
- Khong co loi chan (blocker) trong phien test 30 phut.

### Giai Doan 2 - Mo Rong He Thong Chua Chay Cot Loi (Thang 4-5/2026)
Muc tieu: Tang chieu sau quyet dinh chua chay va do thuc te.

Pham vi:
- Them nhom dam chay theo vat lieu (go, dien, dau/khi voi hanh vi khac nhau).
- Them khoi va ap luc tam nhin/sinh ton (tich hop voi oxygen).
- Nang cap logic binh chua chay de dap lua thuc su theo vung anh huong.
- Them trang thai trang bi: ap suat, hoi, diem nap lai.
- Them co che nguy co bung phat lai va diem nong.

Tieu chi hoan thanh:
- Tung loai chay yeu cau cach xu ly khac nhau.
- Viec chon cong cu anh huong ro ret toi ket qua nhiem vu.
- Cac thong so gameplay co the tinh chinh de trong Inspector.

### Giai Doan 3 - AI Cuu Nan Va Logic Kich Ban (Thang 6-7/2026)
Muc tieu: Chuyen tu sandbox chua chay sang gameplay theo nhiem vu.

Pham vi:
- Them thuc the dan thuong/nan nhan voi tuong tac cuu ho.
- Them AI trang thai co ban: binh thuong, hoang loan, bi thuong, da cuu.
- Them su kien dong: duong bi chan, sap cua thong gio, chay bung thu cap.
- Them he thong cham diem nhiem vu: thoi gian, thiet hai han che, so nguoi cuu.
- Them preset do kho (de, thuong, kho).

Tieu chi hoan thanh:
- Co it nhat 3 bien the kich ban voi ket qua khac nhau.
- Tang gia tri choi lai nho su kien dong.
- Diem so the hien ro nang luc va tien bo nguoi choi.

### Giai Doan 4 - Mo Rong Noi Dung Va Danh Bong UX (Thang 8-9/2026)
Muc tieu: Dat muc noi dung du cho ban phat hanh som (early access style).

Pham vi:
- Xay them 2 map nhiem vu moi.
- Mo rong he tuong tac vat the va object pha huy.
- Cai thien readability UI, key hint va huong dan nhap mon.
- Them menu cai dat (am thanh, do nhay, do hoa, keybind).
- Them luu/tai tien do nguoi choi va nhiem vu.

Tieu chi hoan thanh:
- Co 3 map nhiem vu voi bo cuc va muc tieu khac nhau.
- Nguoi choi moi hoan thanh tutorial ma khong can ho tro ngoai.
- Luu/tai on dinh giua cac phien choi.

### Giai Doan 5 - Toi Uu, QA Va Release Candidate (Thang 10-12/2026)
Muc tieu: Dat chat luong phat hanh on dinh.

Pham vi:
- Profiling va toi uu CPU/GPU/physics cho scene nang.
- Thiet lap regression test cho he lua, trang thai nhiem vu, inventory.
- Them test EditMode va PlayMode trong `Assets/Game/Tests`.
- Chay cac vong bug bash va can bang gameplay.
- Chuan bi pipeline build phat hanh va goi phan phoi.

Tieu chi hoan thanh:
- Thoi gian khung hinh on dinh tren cau hinh muc tieu.
- Khong con loi gameplay muc nghiem trong trong ban RC.
- Test tu dong bao phu cac he thong quan trong cua nhiem vu.

## Uu Tien Ky Thuat
- Refactor he tuong tac va item de giam logic trung lap.
- Chuyen cac hang so gameplay hard-code sang dang du lieu cau hinh.
- Tang kien truc event-driven cho trang thai nhiem vu va UI.
- Phan ro ownership tung he: fire, mission, inventory, UI, AI.

## Moc San Xuat
- M1 (cuoi thang 3/2026): Hoan thien 1 vong lap nhiem vu.
- M2 (cuoi thang 5/2026): Hoan thien he loai chay va dap chay chieu sau.
- M3 (cuoi thang 7/2026): Hoan thien AI cuu nan va su kien dong.
- M4 (cuoi thang 9/2026): Hoan thien mo rong map va UX.
- M5 (cuoi thang 12/2026): Dat ban Release Candidate.

## Sprint Uu Tien Ngay (2 Tuan)
- Dam bao luong build hoat dong xuyen suot tu BootIntro den Map1.
- Lam state machine nhiem vu va UI theo doi muc tieu.
- Cho binh chua chay tac dong truc tiep len `Fire` tuong tu hose (tuning rieng).
- Bo sung toi thieu 5 PlayMode test cho thang/thua nhiem vu va dap lua.
- To chuc 1 buoi playtest noi bo va chot top 10 van de uu tien sua.
