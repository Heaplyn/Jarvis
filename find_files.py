
import os

def main():
    try:
        files = os.listdir('.')
        with open('files.txt', 'w') as f:
            f.write("Files in current directory:\n")
            for file in files:
                f.write(f"- {file}\n")
            
            f.write("\nCurrent working dir: " + os.getcwd() + "\n")
    except Exception as e:
        with open('files.txt', 'w') as f:
            f.write(f"Error: {str(e)}")

if __name__ == '__main__':
    main()
