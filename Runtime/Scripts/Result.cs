using UnityEngine;

public class Result<E>
{
    protected bool isError = false;
    public bool IsError => isError;
    public bool IsOk => !isError;
    
    protected E error;
    public E Error => error;

    protected Result() {}

    public static Result<E> Ok()
    {
        return new Result<E>();
    }

    public static Result<E> Err(E errorValue)
    {
        var toReturn = new Result<E>();
        toReturn.isError = true;
        toReturn.error = errorValue;
        return toReturn;
    }

    public override string ToString()
    {
        return isError ? $"Error! {error}." : "Result is OK.";
    }

    public static implicit operator bool(Result<E> res) => res.IsOk;
}

public class Result : Result<string> 
{
    public static new Result Ok()
    {
        return new Result();
    }

    public static new Result Err(string errorValue)
    {
        var toReturn = new Result();
        toReturn.isError = true;
        toReturn.error = errorValue;
        return toReturn;
    }
}