// 그리드 가로/세로 크기
[System.Serializable]
public struct GridDimensions
{
    public int width;
    public int height;

    public GridDimensions(int width, int height)
    {
        this.width = width;
        this.height = height;
    }
}
