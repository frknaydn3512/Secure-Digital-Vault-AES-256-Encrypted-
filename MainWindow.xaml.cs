using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Diagnostics;

namespace SecretNotepad
{
    public partial class MainWindow : Window
    {
        private string currentSessionPassword;
        private readonly string vaultPath;
        private readonly string hashPath;

        // Kasanın içindeki tüm verileri RAM'de tutacağımız liste
        private List<VaultItem> vaultItems = new List<VaultItem>();

        // Videoları RAM'den oynatamadığımız için geçici bir dosya yolu
        private string tempVideoPath = Path.Combine(Path.GetTempPath(), "gizli_video_temp.mp4");

        public MainWindow()
        {
            InitializeComponent();

            string appDataKlasoru = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appDataKlasoru, "DijitalKasa");
            if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);

            vaultPath = Path.Combine(appFolder, "kasa_verileri.dat");
            hashPath = Path.Combine(appFolder, "kasa_sifresi.hash");

            if (!File.Exists(hashPath))
            {
                LoginButton.Content = "Yeni Şifre Belirle";
                ErrorMessage.Text = "İlk girişiniz! Kasanız için güçlü bir şifre oluşturun.";
                ErrorMessage.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightSkyBlue);
                ErrorMessage.Visibility = Visibility.Visible;
            }
        }

        // --- 1. GİRİŞ VE KASA AÇMA İŞLEMLERİ ---
        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string enteredPassword = PasswordInput.Password;
            if (string.IsNullOrWhiteSpace(enteredPassword)) return;

            if (!File.Exists(hashPath))
            {
                string hashedPassword = CryptoHelper.HashPassword(enteredPassword);
                File.WriteAllText(hashPath, hashedPassword);
                currentSessionPassword = enteredPassword;
                OpenVault();
            }
            else
            {
                string savedHash = File.ReadAllText(hashPath);
                string enteredHash = CryptoHelper.HashPassword(enteredPassword);
                if (savedHash == enteredHash)
                {
                    currentSessionPassword = enteredPassword;
                    OpenVault();
                }
                else
                {
                    ErrorMessage.Text = "Hatalı şifre!";
                    ErrorMessage.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Tomato);
                    ErrorMessage.Visibility = Visibility.Visible;
                }
            }
        }

        private void OpenVault()
        {
            LoginPanel.Visibility = Visibility.Collapsed;
            VaultPanel.Visibility = Visibility.Visible; // Yeni panel görünür oldu
            PasswordInput.Clear();

            if (File.Exists(vaultPath))
            {
                string encryptedText = File.ReadAllText(vaultPath);
                string decryptedText = CryptoHelper.Decrypt(encryptedText, currentSessionPassword);

                if (decryptedText != null)
                {
                    // Çözülen metni JSON'dan Listeye çevir
                    vaultItems = JsonSerializer.Deserialize<List<VaultItem>>(decryptedText) ?? new List<VaultItem>();
                    RefreshList();
                }
                else
                {
                    MessageBox.Show("Kasa açılamadı!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // --- 2. LİSTE GÜNCELLEME VE ÖĞE SEÇME ---
        private void RefreshList()
        {
            ItemsListBox.ItemsSource = null;
            ItemsListBox.ItemsSource = vaultItems;
        }

        // DİKKAT: Başına 'async' kelimesini ekledik
        private async void ItemsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ItemsListBox.SelectedItem is VaultItem selectedItem)
            {
                TitleTextBox.Text = selectedItem.Baslik;

                // Önce her şeyi gizle
                NoteContentTextBox.Visibility = Visibility.Collapsed;
                ImageViewer.Visibility = Visibility.Collapsed;
                VideoPlayer.Visibility = Visibility.Collapsed;
                DocumentViewerPanel.Visibility = Visibility.Collapsed;

                VideoPlayer.Stop();
                VideoPlayer.Source = null;
                VideoPlayer.Close();

                if (File.Exists(tempVideoPath))
                {
                    try { File.Delete(tempVideoPath); }
                    catch { }
                }

                if (selectedItem.Tip == "Metin")
                {
                    NoteContentTextBox.Text = selectedItem.Icerik;
                    NoteContentTextBox.Visibility = Visibility.Visible;
                }
                else if (selectedItem.Tip == "Resim")
                {
                    byte[] imageBytes = Convert.FromBase64String(selectedItem.Icerik);
                    BitmapImage bitmap = new BitmapImage();
                    using (MemoryStream ms = new MemoryStream(imageBytes))
                    {
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                    }
                    ImageViewer.Source = bitmap;
                    ImageViewer.Visibility = Visibility.Visible;
                }
                else if (selectedItem.Tip == "Video")
                {
                    tempVideoPath = Path.Combine(Path.GetTempPath(), $"gizli_video_{Guid.NewGuid()}.mp4");

                    // 1. Ağır işlemi arkaplanda çöz ve diske yaz
                    byte[] videoBytes = await Task.Run(() => Convert.FromBase64String(selectedItem.Icerik));
                    await Task.Run(() => File.WriteAllBytes(tempVideoPath, videoBytes));

                    // 2. Dosyayı oynatıcıya bağla
                    VideoPlayer.Source = new Uri(tempVideoPath);
                    VideoPlayer.Visibility = Visibility.Visible;

                    // 3. EFSANE TAKTİK: Sistemin dosyayı rahat bırakması için yarım saniye (500ms) mola
                    await Task.Delay(500);

                    // 4. Şimdi sorunsuz oynat!
                    VideoPlayer.Play();
                }
                else if (selectedItem.Tip == "Belge")
                {
                    DocumentViewerPanel.Visibility = Visibility.Visible;
                }

            }
        }

        // --- 3. YENİ İÇERİK EKLEME ---
        private void AddNote_Click(object sender, RoutedEventArgs e)
        {
            var newItem = new VaultItem { Id = Guid.NewGuid().ToString(), Baslik = "Yeni Gizli Not", Tip = "Metin", Icerik = "" };
            vaultItems.Add(newItem);
            RefreshList();
            ItemsListBox.SelectedItem = newItem;
        }

        private void AddMedia_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Medya Dosyaları|*.jpg;*.jpeg;*.png;*.mp4;*.avi";

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                string extension = Path.GetExtension(filePath).ToLower();

                // Dosyayı oku ve Base64 metnine çevir (Şifrelemeye uygun hale getir)
                byte[] fileBytes = File.ReadAllBytes(filePath);
                string base64String = Convert.ToBase64String(fileBytes);

                string type = (extension == ".mp4" || extension == ".avi") ? "Video" : "Resim";

                var newItem = new VaultItem { Id = Guid.NewGuid().ToString(), Baslik = Path.GetFileName(filePath), Tip = type, Icerik = base64String };
                vaultItems.Add(newItem);
                RefreshList();
                ItemsListBox.SelectedItem = newItem;
            }
        }

        // --- 4. KAYDETME VE KİLİTLEME ---
        private void SaveVault_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsListBox.SelectedItem is VaultItem selectedItem)
            {
                // Ekrandaki güncel başlığı ve metni objeye kaydet
                selectedItem.Baslik = TitleTextBox.Text;
                if (selectedItem.Tip == "Metin") selectedItem.Icerik = NoteContentTextBox.Text;
            }

            // Tüm listeyi JSON'a çevir, şifrele ve kaydet
            string jsonText = JsonSerializer.Serialize(vaultItems);
            string encryptedText = CryptoHelper.Encrypt(jsonText, currentSessionPassword);
            File.WriteAllText(vaultPath, encryptedText);

            RefreshList();
            MessageBox.Show("Kasa başarıyla güncellendi ve kilitlendi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // --- YENİ: SİLME İŞLEMİ ---
        private void DeleteVaultItem_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsListBox.SelectedItem is VaultItem selectedItem)
            {
                // Yanlışlıkla silmeye karşı güvenlik sorusu
                MessageBoxResult result = MessageBox.Show($"'{selectedItem.Baslik}' adlı dosyayı kasadan tamamen silmek istediğinize emin misiniz?\n(Bu işlem geri alınamaz!)", "Kalıcı Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // 1. Eğer silinen şey video ise arka planda oynatmayı durdur ve dosyayı serbest bırak
                    VideoPlayer.Stop();
                    VideoPlayer.Source = null;
                    VideoPlayer.Close();
                    ImageViewer.Source = null; // Resmi de ekrandan temizle

                    // 2. Varsa arkada kalan geçici (temp) video dosyasını da hemen yok et
                    if (File.Exists(tempVideoPath))
                    {
                        try { File.Delete(tempVideoPath); } catch { }
                    }

                    // 3. Listeden öğeyi sil ve sol menüyü yenile
                    vaultItems.Remove(selectedItem);
                    RefreshList();

                    // 4. Sağ ekranı tertemiz yap
                    TitleTextBox.Text = "Başlık Seçin veya Ekleyin";
                    NoteContentTextBox.Text = "";
                    NoteContentTextBox.Visibility = Visibility.Collapsed;
                    ImageViewer.Visibility = Visibility.Collapsed;
                    VideoPlayer.Visibility = Visibility.Collapsed;
                    DocumentViewerPanel.Visibility = Visibility.Collapsed;

                    // 5. EN ÖNEMLİSİ: Silinmiş yeni listeyi anında şifreleyip diske yaz (Kalıcı olarak yok et)
                    string jsonText = JsonSerializer.Serialize(vaultItems);
                    string encryptedText = CryptoHelper.Encrypt(jsonText, currentSessionPassword);
                    File.WriteAllText(vaultPath, encryptedText);

                    MessageBox.Show("Dosya kasadan kalıcı olarak silindi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Lütfen silmek için sol menüden bir dosya seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LockButton_Click(object sender, RoutedEventArgs e)
        {
            // Güvenlik: RAM'deki her şeyi temizle
            currentSessionPassword = null;
            vaultItems.Clear();
            ItemsListBox.ItemsSource = null;
            VideoPlayer.Stop();
            if (File.Exists(tempVideoPath)) File.Delete(tempVideoPath); // Geçici videoyu sil

            VaultPanel.Visibility = Visibility.Collapsed;
            LoginPanel.Visibility = Visibility.Visible;

            LoginButton.Content = "Kilidi Aç";
            ErrorMessage.Visibility = Visibility.Collapsed;
        }

        // --- YENİ: BELGE EKLEME METODU ---
        private void AddDocument_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            // İzin verilen belge türleri
            openFileDialog.Filter = "Belge Dosyaları|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.txt;*.zip;*.rar";

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                string extension = Path.GetExtension(filePath).ToLower(); // .pdf, .docx vb.

                byte[] fileBytes = File.ReadAllBytes(filePath);
                string base64String = Convert.ToBase64String(fileBytes);

                var newItem = new VaultItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Baslik = Path.GetFileNameWithoutExtension(filePath), // Sadece ismini al
                    Tip = "Belge",
                    Icerik = base64String,
                    Uzanti = extension // Uzantıyı ayrıca kaydet
                };

                vaultItems.Add(newItem);
                RefreshList();
                ItemsListBox.SelectedItem = newItem;
            }
        }

        // --- YENİ: BELGE AÇMA METODU ---
        private async void OpenDocument_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsListBox.SelectedItem is VaultItem selectedItem && selectedItem.Tip == "Belge")
            {
                try
                {
                    // Uzantısıyla beraber temp bir dosya oluştur
                    string tempDocPath = Path.Combine(Path.GetTempPath(), $"gizli_belge_{Guid.NewGuid()}{selectedItem.Uzanti}");

                    // Ağır işlemi arkaplanda yap (Arayüz donmasın)
                    byte[] docBytes = await Task.Run(() => Convert.FromBase64String(selectedItem.Icerik));
                    await Task.Run(() => File.WriteAllBytes(tempDocPath, docBytes));

                    // Dosyayı Windows'un kendi programıyla (Word, PDF Okuyucu vs.) aç
                    Process.Start(new ProcessStartInfo(tempDocPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Belge açılırken bir hata oluştu: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // --- YENİ: ŞİFRE DEĞİŞTİRME İŞLEMLERİ ---
        private void ShowChangePassword_Click(object sender, RoutedEventArgs e)
        {
            // Paneli göster ve içini temizle
            ChangePasswordPanel.Visibility = Visibility.Visible;
            OldPasswordBox.Clear();
            NewPasswordBox.Clear();
            ConfirmNewPasswordBox.Clear();
            ChangePasswordError.Visibility = Visibility.Collapsed;
        }

        private void CancelChangePassword_Click(object sender, RoutedEventArgs e)
        {
            // İptal edilirse paneli gizle
            ChangePasswordPanel.Visibility = Visibility.Collapsed;
        }

        private void ConfirmChangePassword_Click(object sender, RoutedEventArgs e)
        {
            string oldPass = OldPasswordBox.Password;
            string newPass = NewPasswordBox.Password;
            string confirmPass = ConfirmNewPasswordBox.Password;

            // 1. Boş alan kontrolü
            if (string.IsNullOrWhiteSpace(oldPass) || string.IsNullOrWhiteSpace(newPass))
            {
                ChangePasswordError.Text = "Şifre alanları boş bırakılamaz!";
                ChangePasswordError.Visibility = Visibility.Visible;
                return;
            }

            // 2. Yeni şifreler eşleşiyor mu?
            if (newPass != confirmPass)
            {
                ChangePasswordError.Text = "Yeni şifreler birbiriyle eşleşmiyor!";
                ChangePasswordError.Visibility = Visibility.Visible;
                return;
            }

            // 3. Eski şifre doğru mu? (Gerçekten kasanın sahibi mi?)
            string savedHash = File.ReadAllText(hashPath);
            string enteredOldHash = CryptoHelper.HashPassword(oldPass);

            if (savedHash != enteredOldHash)
            {
                ChangePasswordError.Text = "Mevcut şifrenizi yanlış girdiniz!";
                ChangePasswordError.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                // İŞTE BÜYÜK OPERASYON: Eski kasayı yak, yeni şifreyle baştan inşa et!

                // A) Yeni şifrenin özetini kaydet
                string newHash = CryptoHelper.HashPassword(newPass);
                File.WriteAllText(hashPath, newHash);

                // B) RAM'deki verileri yeni şifreyle şifreleyip diske yaz
                string jsonText = JsonSerializer.Serialize(vaultItems);
                string encryptedText = CryptoHelper.Encrypt(jsonText, newPass);
                File.WriteAllText(vaultPath, encryptedText);

                // C) Geçerli oturum şifresini güncelle ki program hata vermeden çalışmaya devam etsin
                currentSessionPassword = newPass;

                MessageBox.Show("Tebrikler! Şifreniz başarıyla değiştirildi.\nTüm dosyalarınız yeni şifrenizle baştan kilitlendi.", "Güvenlik Güncellemesi", MessageBoxButton.OK, MessageBoxImage.Information);
                ChangePasswordPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Şifre değiştirilirken bir hata oluştu: " + ex.Message, "Kritik Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}