namespace MoonlightPost
{
    /// <summary>우체국 옆 강화 작업대. 말을 걸면 강화 화면(UpgradeMenu)이 열린다.</summary>
    public class Workbench : Interactable
    {
        public override string DisplayName => "강화 작업대";
        public override string Prompt => "강화 작업대 (장비 강화)";
        public override void Interact(PlayerController player) => UpgradeMenu.Open();
    }
}
