namespace CoreAPI.Core.Models;

using CoreAPI.Core.Helpers;
using CoreAPI.Core.Exceptions;
using CoreAPI.Core.Interfaces;
using System.Text;
using System.ComponentModel.DataAnnotations;

public abstract class Production : IDisplayable
{
    public const uint MIN_EMPLOYEES_NUMBER = 1;
    public const uint MAX_EMPLOYEES_NUMBER = 10_000;

    //! NAME
    private string _name;
    
    [Required(ErrorMessage = "Name is required.")] // Name cannot be null or empty.
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Name must be between 3 and 100 characters.")]
    public string Name
    {
        get => _name;
        set => ValidatorHelper.SetValueWithValidation(this, ref _name, nameof(Name), value); // Validation and assignment
    }

    //! MANAGER
    private string _manager;

    [Required(ErrorMessage = "Manager is required.")] // Manager cannot be null or empty.
    public string Manager
    {
        get => _manager;
        set => ValidatorHelper.SetValueWithValidation(this, ref _manager, nameof(Manager), value); // Validation and assignment
    }

    //! WORKER_COUNT
    private uint _workerCount;

    [Range(MIN_EMPLOYEES_NUMBER, MAX_EMPLOYEES_NUMBER, ErrorMessage = "WorkerCount must be greater than 0.")]
    public uint WorkerCount
    {
        get => _workerCount;
        set => ValidatorHelper.SetValueWithValidation(this, ref _workerCount, nameof(WorkerCount), value); // Validation and assignment
    }

    //! PRODUCT_LIST
    private List<string> _productList;

    [MinLength(1, ErrorMessage = "ProductList must contain at least one product.")]
    public List<string> ProductList
    {
        get => _productList;
        set => ValidatorHelper.SetValueWithValidation(this, ref _productList, nameof(ProductList), value); // Validation and assignment
    }

    // To initialize an empty object
    protected Production() 
    { 
        _name = string.Empty;
        _manager = string.Empty;
        _workerCount = 0;
        _productList = new List<string>();
    }

    public Production(string name, string manager, uint workerCount, List<string> productList)
    {
        _name = name;
        _manager = manager;
        _workerCount = workerCount;
        _productList = productList;
        
        ValidatorHelper.ValidateObject(this);
    }

    public Production(Production other)
    {
        _name = other._name;
        _manager = other._manager;
        _workerCount = other._workerCount;
        _productList = new List<string>(other._productList);
    }

    // Indexer for accessing the product list by index
    // In C#, indexers do not support validation attributes directly like regular properties.
    public string this[int index]
    {
        get
        {
            if (index < 0 || index >= _productList.Count)
            {
                throw new ProductOutOfRangeException("Index is out of range.");
            }

            return _productList[index];
        }
        set
        {
            if (index < 0 || index >= _productList.Count)
            {
                throw new ProductOutOfRangeException("Index is out of range.");
            }

            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidProductException("Product name cannot be null or empty.");
            }

            _productList[index] = value;
        }
    }

    // Attempt to add a product without throwing exceptions
    public bool TryAddProduct(string product)
    {
        if (IsInvalidProduct(product))
        {
            return false; // The product is not added if it is invalid
        }

        _productList.Add(product);
        return true;
    }

    // Add a product with an exception if the operation is not successful
    public void AddProduct(string product)
    {
        if (!TryAddProduct(product))
        {
            throw new InvalidProductException("Product name cannot be null, empty, or a duplicate.");
        }
    }

    public bool RemoveProduct(string product)
    {
        return ProductList.Remove(product);
    }

    public string GetProductionInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Production: {Name}");
        sb.AppendLine($"Manager: {Manager}");
        sb.AppendLine($"Number of workers: {WorkerCount}\n");
        sb.Append(GetProductionList());

        return sb.ToString();
    }

    public string GetShortProductionInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Production: {Name}");
        return sb.ToString();
    }

    public virtual void ShowInfo(Action<string> output)
    {
        string info = GetProductionInfo();
        output(info); // passing the output string
    }

    public virtual void ShowShortInfo(Action<string> output)
    {
        string info = GetShortProductionInfo();
        output(info); // passing the output string
    }

    public override string ToString()
    {
        return GetProductionInfo();
    }

    public string GetProductionList()
    {
        return FormatList(ProductList, "The list of the nomenclature of manufactured products:", item => $" - {item}");
    }

    public void ShowProductionList(Action<string> output)
    {
        string list = GetProductionList();
        output(list);
    }

    //! AUXILIARY METHODS
    protected string FormatList<T>(IEnumerable<T> list, string title, Func<T, string> formatItem)
    {
        var sb = new StringBuilder();
        sb.AppendLine(title);

        if (list == null || !list.Any())
        {
            sb.AppendLine("- No items available.");
        }
        else
        {
            foreach (var item in list)
            {
                sb.AppendLine(formatItem(item));
            }
        }

        return sb.ToString();
    }

    // Checking for an acceptable product
    private bool IsInvalidProduct(string product)
    {
        return string.IsNullOrEmpty(product) || _productList.Contains(product);
    }
}