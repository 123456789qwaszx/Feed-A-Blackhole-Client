namespace BlackHole.Unity
{
    // TextBindings(Presentation)가 닿지 않는 글자를 가진 화면. 등록된 화면이면 UISettingsSync가 시작할 때 한 번,
    // 글자 크기 설정이 바뀔 때마다 부른다(닫힌 화면도). 화면은 제 글자를 ScaledTexts에 넣어 두고 배율만 넘긴다.
    // TextBindings에 역할로 등록된 글자는 Theme가 "역할 크기 × 배율"로 맞추므로 여기에 넣지 않는다(두 번 곱해진다).
    public interface ITextScaleView
    {
        void ApplyTextScale(float scale);
    }
}
