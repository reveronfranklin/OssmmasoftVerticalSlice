namespace OssmmasoftVerticalSlice.Helpers
{
    public class ResultDto<T>
    {

        public ResultDto(T data)
        {
            Data = data;

        }

        public T? Data { get; set; }
        public bool IsValid  { get; set; }
        public string LinkData { get; set; }
        public string LinkDataArlternative { get; set; }
        public string Message { get; set; } = string.Empty;
        public int Page { get; set; }
        public int TotalPage { get; set; }
        
        public int CantidadRegistros { get; set; }
        
        public decimal Total1 { get; set; }
        
        public decimal Total2 { get; set; }
        public decimal Total3 { get; set; }
        public decimal Total4 { get; set; }

    }


   

}
