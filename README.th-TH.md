# PvP Chronicle

[English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [한국어](README.ko-KR.md) | [Русский](README.ru-RU.md) | [Português (Brasil)](README.pt-BR.md) | [Español](README.es-ES.md) | **ไทย** | [Tiếng Việt](README.vi-VN.md) | [日本語](README.ja-JP.md) | [Deutsch](README.de-DE.md) | [Français](README.fr-FR.md)

PvP Chronicle เป็นปลั๊กอิน Dalamud สำหรับบันทึกและทบทวนการแข่งขัน PvP ใน FINAL FANTASY XIV รองรับประวัติและรีเพลย์ของ Frontline และ Crystalline Conflict เป้าหมายบนแผนที่ สถิติประสิทธิภาพ และการตรวจสอบความสมบูรณ์ของพฤติกรรมที่จำกัดอยู่ภายในหลักฐานซึ่งสังเกตได้เท่านั้น

## การติดตั้ง

1. ติดตั้งและเปิด XIVLauncher โดยเปิดใช้งาน Dalamud
2. เปิด **Dalamud Settings → Experimental**
3. เพิ่ม URL คลังปลั๊กอินแบบกำหนดเองต่อไปนี้:

   ```text
   https://raw.githubusercontent.com/chefiffer/dalamud-plugins/main/repo.json
   ```

4. เปิด Plugin Installer ค้นหา **Battlefield Frontline Collector** แล้วเลือก **Install**

## คุณสมบัติเด่น

- ประวัติการแข่งขันในเครื่องและรีเพลย์โดยละเอียดสำหรับ Frontline และ Crystalline Conflict
- ไทม์ไลน์คะแนน KDA ลำดับการต่อสู้ การล้ม การเคลื่อนที่ เครื่องหมาย และเป้าหมายที่สังเกตได้
- ปฏิทิน PvP การหมุนเวียนแผนที่ กลไกเฉพาะแผนที่ และ UI ที่แปลเป็นหลายภาษา
- เลือกอัปโหลดเพื่อรับ Logs และการแจกแจงอ้างอิงที่สร้างโดยเซิร์ฟเวอร์

PvP Chronicle ไม่ทำให้การเล่นเป็นอัตโนมัติ ไม่สั่งใช้แอ็กชันต่อสู้ ไม่เปิดเผยสถานะเกมที่ซ่อนอยู่ และไม่ถือว่าการสังเกตที่ไม่สมบูรณ์เป็นหลักฐานยืนยันการโกง

## การร่วมพัฒนา

Fork คลังนี้ สร้าง branch ที่มุ่งเน้นการเปลี่ยนแปลงเดียว เพิ่มการทดสอบสำหรับพฤติกรรมที่ได้รับผลกระทบ แล้วเปิด pull request ไปยัง `main`

อย่าส่งไฟล์บันทึกการแข่งขัน ชุดข้อมูลผู้เล่น access token ที่อยู่เซิร์ฟเวอร์ส่วนตัว หรือข้อมูลส่วนบุคคลของผู้อื่น รายงานปัญหาความปลอดภัยตาม [`SECURITY.md`](SECURITY.md)

## สัญญาอนุญาต

ซอร์สโค้ดไคลเอนต์ PvP Chronicle ในคลังนี้เผยแพร่ภายใต้ [GNU Affero General Public License v3.0](LICENSE)

สัญญาอนุญาตนี้ใช้กับซอร์สโค้ดไคลเอนต์ต้นฉบับของ PvP Chronicle และไฟล์ที่ระบุชัดว่าเป็น AGPL-3.0-only เท่านั้น ไม่ได้เปลี่ยนสัญญาอนุญาตของเนื้อหา FINAL FANTASY XIV ไลบรารีภายนอก หรือแบบอักษร ซึ่งยังอยู่ภายใต้เงื่อนไขของเจ้าของสิทธิ์แต่ละราย

ชื่อ ภาพ และไอคอนของ FINAL FANTASY XIV ยังคงเป็นทรัพย์สินของ Square Enix ดูการระบุที่มาของเนื้อหาเกมและประกาศของบุคคลที่สามได้ที่ [`plugin/THIRD-PARTY-NOTICES.md`](plugin/THIRD-PARTY-NOTICES.md)

PvP Chronicle เป็นโครงการชุมชนอิสระที่ไม่แสวงหากำไร และไม่มีความเกี่ยวข้องหรือการรับรองจาก Square Enix
