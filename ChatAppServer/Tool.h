#ifndef TOOL_HEADER
#include <iostream>
#include<string>
#include <fstream>
#include <thread>
#include <unordered_map>
#include <string.h>
using namespace std;

int SumOfSeries2(int a)
{
	double result = a;
	double n = a;
	while (n != 0)
	{
		if (n > 1)
		{
			result += n / 2;
			n = n / 2;
		}
		else if (n <= 1)
		{
			n = 0;
		}
	}
	return (int)result;
}
int Compare(string x, string y)
{
	if (x.length() == y.length())
	{
		int check = 0;
		for (int i = 0; i < x.length(); i++)
		{
			if (x[i] != y[i])
			{
				check++;
			}
		}
		if (check != 0)
		{
			return 1;
		}
		else if (check == 0)
		{
			return 0;
		}
	}
}
bool FindVariable(string a, string var)
{
	int check = 1;
	string temp;
	//cout << "Length " << a.length() << endl;
	for (int i = 0; i < a.length(); i++)
	{
		if (a[i] != '/')
		{
			temp += a[i];
		}
		else if (a[i] == '/')
		{
			if (temp == var)
			{
				check = 0;
				break;
			}
			temp.clear();
		}
		/*cout << temp<< endl;
		cout << "check = " << check << endl;*/

	}
	/*if (check == 0)
	{
		cout << "True " << endl;
		return true;
	}
	else if (check != 0)
	{
		cout << "False " << endl;
		return false;
	}*/
	if (temp == var)
	{
		check = 0;
	}
	return (check == 0);
}
void RemoveEnter(string& str)
{
	str.erase(remove(str.begin(), str.end(), '\n'), str.end());
	str.erase(remove(str.begin(), str.end(), '\t'), str.end());
}
int BinaryStringToInteger(string String)
{
	int result = 0;
	int pos = 7;
	if (String.length() == 8)
	{
		for (char c : String)
		{
			if (c == '0')
			{
				result += 0;
			}
			else if (c == '1')
			{
				result += pow(2, static_cast<double>(pos));
			}
			pos--;
		}
	}
	else if (String.length() < 8)
	{
		int n = 8 - String.length();
		string temp;
		for (int i = 0; i < n; i++)
		{
			temp += "0";
		}
		temp += String;
		for (char c : temp)
		{
			if (c == '0')
			{
				result += 0;
			}
			else if (c == '1')
			{
				result += pow(2, static_cast<double>(pos));
			}
			pos--;
		}
	}
	return result;
}
//string stringToBinary(const string& input) {
//	string binaryString;
//	for (char c : input) {
//		binaryString += bitset<1>(c).to_string(); // Convert each character to its 1-bit binary representation
//	}
//	return binaryString;
//}
//vector<unsigned char> binaryStringToBytes(const string& binaryString) {
//	vector<unsigned char> bytes;
//	for (size_t i = 0; i < binaryString.size(); i += 8) {
//		string byteString = binaryString.substr(i, 8); // Extract 8 bits (1 byte) at a time
//		unsigned char byte = bitset<1>(byteString).to_ulong(); // Convert the byte string to unsigned char
//		bytes.push_back(byte); // Store the byte in the vector
//	}
//	return bytes;
//}
#endif // !TOOL_HEADER

