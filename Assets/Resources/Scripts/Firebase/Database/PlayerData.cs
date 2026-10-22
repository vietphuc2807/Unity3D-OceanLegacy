using System.Collections.Generic;
using Firebase.Firestore;

[FirestoreData]
public class PlayerData
{
    [FirestoreProperty]
    public string UID { get; private set; }

    [FirestoreProperty]
    public string Email { get; private set; }

    [FirestoreProperty]
    public string Name { get; private set; }

    [FirestoreProperty]
    public string Nickname { get; private set; }

    [FirestoreProperty]
    public bool Verified { get; private set; }

    /// <summary>
    /// Du lieu 4 be trung bay ca o Museum - moi phan tu la 1 be
    /// (TankId, ItemId cua con ca dang trung bay, va size luc cau
    /// duoc). Be nao chua duoc dat ca thi don gian KHONG co entry
    /// tuong ung trong list nay (khong can entry rong).
    ///
    /// Set qua private set giong cac property khac (Firestore SDK van
    /// gan duoc qua reflection luc deserialize) - de SUA du lieu, dung
    /// PlayerDatabase.SaveAquariumTank()/RemoveAquariumTank() thay vi
    /// gan truc tiep property nay tu ben ngoai.
    ///
    /// Co the la null voi cac tai khoan da tao TRUOC KHI co tinh nang
    /// nay (Firestore document cu chua co field nay) - moi noi doc
    /// field nay deu phai tu kiem tra null.
    /// </summary>
    [FirestoreProperty]
    public List<AquariumTankEntry> AquariumTanks { get; private set; }

    /// <summary>
    /// Tong Fame Point cua TAT CA con ca dang trung bay trong cac be
    /// cua Museum nay CONG LAI - duoc CLIENT tinh (dua vao
    /// AquariumTanks + FishData.GetFamePoint(), xem MuseumStatsUI) roi
    /// day len day MOI KHI co thay doi thuc su (dat/lay ca), KHONG
    /// phai nguon du lieu goc - chi la 1 ban SAO CHEP (denormalized)
    /// dung RIENG de Firestore query/sap xep bang xep hang
    /// (OrderByDescending) duoc hieu qua, thay vi phai tai TOAN BO
    /// PlayerData cua MOI nguoi choi trong game roi tu tinh + sap xep
    /// o client.
    ///
    /// Xem PlayerDatabase.SaveMuseumStats() / GetMuseumLeaderboard()
    /// va AquariumSaveService.SaveMuseumStats().
    ///
    /// Mac dinh 0 - cac tai khoan da tao TRUOC KHI co tinh nang nay se
    /// tu duoc cap nhat gia tri dung ngay lan dau ho vao lai Museum
    /// (MuseumStatsUI tinh lai tu AquariumTanks va luu de).
    /// </summary>
    [FirestoreProperty]
    public int TotalFamePoint { get; private set; }

    public PlayerData() { }

    public PlayerData(string uid, string email, string name, string nickname)
    {
        UID = uid;
        Email = email;
        Name = name;
        Nickname = nickname;

        Verified = false;

        AquariumTanks = new List<AquariumTankEntry>();
        TotalFamePoint = 0;
    }
}