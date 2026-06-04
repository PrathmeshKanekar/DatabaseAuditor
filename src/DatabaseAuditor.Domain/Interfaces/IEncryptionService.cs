namespace DatabaseAuditor.Domain.Interfaces;

public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
    byte[] EncryptBytes(byte[] data);
    byte[] DecryptBytes(byte[] data);
}