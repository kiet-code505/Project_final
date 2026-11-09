import org.springframework.security.crypto.bcrypt.BCrypt;
import java.util.UUID;

class JavaPasswordFixture {
    public static void main(String[] args) {
        String password = "Aa1!" + UUID.randomUUID().toString();
        String hash = BCrypt.hashpw(password, BCrypt.gensalt(10));
        System.out.print("{\"password\":\"" + password + "\",\"hash\":\"" + hash + "\"}");
    }
}
